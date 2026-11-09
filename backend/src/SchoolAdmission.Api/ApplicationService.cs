using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Data;

namespace SchoolAdmission.Api;

/// Guardian applies (PENDING) -> admin approves (seat held, timer starts) -> guardian confirms the fee
/// through the normal POST /holds/{id}/confirm endpoint.
public class ApplicationService(AppDb db, AdmissionService adm, HoldOptions options)
{
    private static bool ValidNid(string nid) => Regex.IsMatch(nid, @"^(\d{10}|\d{13}|\d{17})$");

    private async Task<List<ApplicationDto>> ToDtosAsync(List<Application> apps)
    {
        var now = DateTime.UtcNow;
        var classIds = apps.Select(a => a.ClassId).Distinct().ToList();
        var holdIds = apps.Where(a => a.HoldId != null).Select(a => a.HoldId!).ToList();
        var classes = await db.Classes.AsNoTracking().Where(c => classIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name);
        var holds = await db.Holds.AsNoTracking().Where(h => holdIds.Contains(h.Id)).ToDictionaryAsync(h => h.Id);
        return apps.Select(a =>
        {
            Hold? h = a.HoldId != null && holds.TryGetValue(a.HoldId, out var x) ? x : null;
            return new ApplicationDto(a.Id, a.GuardianName, a.StudentName, a.Nid, a.StudentId, a.ClassId,
                classes.GetValueOrDefault(a.ClassId, ""), a.Status, a.HoldId, h?.EffectiveStatus(now), h?.ExpiresAt, a.CreatedAt);
        }).ToList();
    }

    public async Task<IResult> CreateAsync(CreateApplicationRequest r)
    {
        var guardian = r.GuardianName?.Trim() ?? "";
        var student = r.StudentName?.Trim() ?? "";
        var nid = r.Nid?.Trim() ?? "";
        if (guardian.Length is 0 or > 100) return AdmissionService.Error(400, "Guardian name is required (up to 100 characters)");
        if (student.Length is 0 or > 100) return AdmissionService.Error(400, "Student name is required (up to 100 characters)");
        if (!ValidNid(nid)) return AdmissionService.Error(400, "NID number must be 10, 13 or 17 digits");
        if (string.IsNullOrWhiteSpace(r.ClassId) || !await db.Classes.AnyAsync(c => c.Id == r.ClassId))
            return AdmissionService.Error(404, "Class not found");

        // The same child (same NID + name) keeps one Student ID across applications.
        var lower = student.ToLower();
        var studentId = await db.Applications.Where(a => a.Nid == nid && a.StudentName.ToLower() == lower)
            .Select(a => a.StudentId).FirstOrDefaultAsync();
        studentId ??= $"{DateTime.UtcNow.Year}{await db.Database.SqlQueryRaw<long>("SELECT nextval('student_id_seq') AS \"Value\"").SingleAsync():D10}";

        var open = await db.Applications.AnyAsync(a => a.StudentId == studentId && a.ClassId == r.ClassId &&
            (a.Status == AppStatus.Pending || a.Status == AppStatus.Approved));
        if (open) return AdmissionService.Error(409, "This student already has an open application for this class");

        var app = new Application
        {
            Id = AdmissionService.NewId("a_"), GuardianName = guardian, StudentName = student, Nid = nid,
            StudentId = studentId, ClassId = r.ClassId!, Status = AppStatus.Pending, CreatedAt = DateTime.UtcNow
        };
        db.Applications.Add(app);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) { return AdmissionService.Error(409, "This student already has an open application for this class"); }
        return Results.Created($"/applications/{app.Id}", (await ToDtosAsync(new List<Application> { app }))[0]);
    }

    public async Task<IResult> ByNidAsync(string? nid)
    {
        nid = nid?.Trim() ?? "";
        if (!ValidNid(nid)) return AdmissionService.Error(400, "NID number must be 10, 13 or 17 digits");
        var apps = await db.Applications.AsNoTracking().Where(a => a.Nid == nid).OrderByDescending(a => a.CreatedAt).ToListAsync();
        return Results.Ok(await ToDtosAsync(apps));
    }

    public async Task<IResult> AllAsync()
    {
        var apps = await db.Applications.AsNoTracking().OrderByDescending(a => a.CreatedAt).ToListAsync();
        return Results.Ok(await ToDtosAsync(apps));
    }

    public async Task<IResult> ApproveAsync(string id)
    {
        var first = await db.Applications.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
        if (first is null) return AdmissionService.Error(404, "Application not found");

        await using var tx = await db.Database.BeginTransactionAsync();
        var cls = await adm.LockClassAsync(first.ClassId); // same row lock as every other seat change
        var app = await db.Applications.FirstAsync(a => a.Id == id);
        var now = DateTime.UtcNow;

        if (app.Status == AppStatus.Rejected) return AdmissionService.Error(409, "This application was rejected");
        if (app.HoldId != null)
        {
            var old = await db.Holds.AsNoTracking().FirstAsync(h => h.Id == app.HoldId);
            if (old.Status == HoldStatus.Confirmed || (old.Status == HoldStatus.Held && old.ExpiresAt > now))
                return Results.Ok((await ToDtosAsync(new List<Application> { app }))[0]); // already approved: idempotent
        }

        await db.Holds.Where(h => h.ClassId == app.ClassId && h.Status == HoldStatus.Held && h.ExpiresAt <= now)
            .ExecuteUpdateAsync(s => s.SetProperty(h => h.Status, HoldStatus.Expired));
        var (held, confirmed) = await adm.CountSeatsAsync(app.ClassId, now);
        if (held + confirmed >= cls!.TotalSeats) return AdmissionService.Error(409, "No seats available to approve this application");

        var hold = new Hold
        {
            Id = AdmissionService.NewId("h_"), ClassId = app.ClassId, StudentId = app.StudentId, Status = HoldStatus.Held,
            CreatedAt = now, ExpiresAt = now.AddSeconds(options.DurationSeconds)
        };
        db.Holds.Add(hold);
        app.Status = AppStatus.Approved;
        app.HoldId = hold.Id;
        app.DecidedAt = now;
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) { return AdmissionService.Error(409, "This student already holds a seat in this class"); }
        await tx.CommitAsync();
        return Results.Ok((await ToDtosAsync(new List<Application> { app }))[0]);
    }

    public async Task<IResult> RejectAsync(string id)
    {
        var first = await db.Applications.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
        if (first is null) return AdmissionService.Error(404, "Application not found");

        await using var tx = await db.Database.BeginTransactionAsync();
        await adm.LockClassAsync(first.ClassId);
        var app = await db.Applications.FirstAsync(a => a.Id == id);
        if (app.Status == AppStatus.Rejected) return Results.Ok((await ToDtosAsync(new List<Application> { app }))[0]);
        if (app.Status != AppStatus.Pending) return AdmissionService.Error(409, "Only applications waiting for approval can be rejected");
        app.Status = AppStatus.Rejected;
        app.DecidedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok((await ToDtosAsync(new List<Application> { app }))[0]);
    }
}
