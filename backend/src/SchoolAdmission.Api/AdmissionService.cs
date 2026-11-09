using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Data;

namespace SchoolAdmission.Api;

public class AdmissionService(AppDb db, HoldOptions options)
{
    internal static string NewId(string prefix) => prefix + Guid.NewGuid().ToString("N")[..12];
    internal static IResult Error(int status, string message) => Results.Json(new { error = message }, statusCode: status);

    private static ClassDto ToDto(SchoolClass c, int held, int confirmed) =>
        new(c.Id, c.Name, c.TotalSeats, held, confirmed, Math.Max(0, c.TotalSeats - held - confirmed));

    // ---------- Classes ----------

    public async Task<IResult> CreateClassAsync(CreateClassRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) return Error(400, "name is required");
        if (req.TotalSeats <= 0) return Error(400, "totalSeats must be greater than 0");

        var cls = new SchoolClass
        {
            Id = NewId("c_"),
            Name = req.Name.Trim(),
            TotalSeats = req.TotalSeats,
            CreatedAt = DateTime.UtcNow
        };
        db.Classes.Add(cls);
        await db.SaveChangesAsync();
        return Results.Created($"/classes/{cls.Id}", ToDto(cls, 0, 0));
    }

    public async Task<IResult> ListClassesAsync()
    {
        var now = DateTime.UtcNow;
        var classes = await db.Classes.AsNoTracking().OrderBy(c => c.CreatedAt).ToListAsync();

        // Only CONFIRMED and not-yet-expired HELD holds occupy a seat.
        var counts = await db.Holds.AsNoTracking()
            .Where(h => h.Status == HoldStatus.Confirmed || (h.Status == HoldStatus.Held && h.ExpiresAt > now))
            .GroupBy(h => new { h.ClassId, h.Status })
            .Select(g => new { g.Key.ClassId, g.Key.Status, N = g.Count() })
            .ToListAsync();

        var result = classes.Select(c =>
        {
            var held = counts.Where(x => x.ClassId == c.Id && x.Status == HoldStatus.Held).Sum(x => x.N);
            var confirmed = counts.Where(x => x.ClassId == c.Id && x.Status == HoldStatus.Confirmed).Sum(x => x.N);
            return ToDto(c, held, confirmed);
        });
        return Results.Ok(result);
    }

    public async Task<IResult> GetClassAsync(string classId)
    {
        var cls = await db.Classes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == classId);
        if (cls is null) return Error(404, "Class not found");
        var (held, confirmed) = await CountSeatsAsync(classId, DateTime.UtcNow);
        return Results.Ok(ToDto(cls, held, confirmed));
    }

    internal async Task<(int held, int confirmed)> CountSeatsAsync(string classId, DateTime now)
    {
        var held = await db.Holds.CountAsync(h => h.ClassId == classId && h.Status == HoldStatus.Held && h.ExpiresAt > now);
        var confirmed = await db.Holds.CountAsync(h => h.ClassId == classId && h.Status == HoldStatus.Confirmed);
        return (held, confirmed);
    }

    /// Takes a row lock on the class: SELECT ... FOR UPDATE.
    /// Every seat-changing operation for one class is serialized by this lock, in the database,
    /// so it is correct for concurrent requests AND for several backend processes.
    internal async Task<SchoolClass?> LockClassAsync(string classId)
    {
        var rows = await db.Classes
            .FromSqlInterpolated($"SELECT * FROM classes WHERE \"Id\" = {classId} FOR UPDATE")
            .ToListAsync();
        return rows.FirstOrDefault();
    }

    // ---------- Holds ----------

    public async Task<IResult> CreateHoldAsync(string classId, string studentId)
    {
        await using var tx = await db.Database.BeginTransactionAsync();

        var cls = await LockClassAsync(classId);
        if (cls is null) return Error(404, "Class not found");

        var now = DateTime.UtcNow;

        // Free seats of expired holds in this class (also keeps the unique index valid).
        await db.Holds
            .Where(h => h.ClassId == classId && h.Status == HoldStatus.Held && h.ExpiresAt <= now)
            .ExecuteUpdateAsync(s => s.SetProperty(h => h.Status, HoldStatus.Expired));

        var alreadyActive = await db.Holds.AnyAsync(h =>
            h.ClassId == classId && h.StudentId == studentId &&
            (h.Status == HoldStatus.Held || h.Status == HoldStatus.Confirmed));
        if (alreadyActive)
        {
            await tx.CommitAsync();
            return Error(409, "You already have an active application for this class");
        }

        var (held, confirmed) = await CountSeatsAsync(classId, now);
        if (held + confirmed >= cls.TotalSeats)
        {
            await tx.CommitAsync();
            return Error(409, "No seats available");
        }

        var hold = new Hold
        {
            Id = NewId("h_"),
            ClassId = classId,
            StudentId = studentId,
            Status = HoldStatus.Held,
            CreatedAt = now,
            ExpiresAt = now.AddSeconds(options.DurationSeconds)
        };
        db.Holds.Add(hold);
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        return Results.Created($"/holds/{hold.Id}",
            new HoldCreatedDto(hold.Id, hold.ClassId, hold.StudentId, hold.Status, hold.ExpiresAt));
    }

    public async Task<IResult> GetHoldAsync(string holdId)
    {
        var hold = await db.Holds.AsNoTracking().FirstOrDefaultAsync(h => h.Id == holdId);
        if (hold is null) return Error(404, "Hold not found");
        return Results.Ok(new HoldDto(hold.Id, hold.ClassId, hold.StudentId, hold.EffectiveStatus(DateTime.UtcNow), hold.ExpiresAt));
    }

    public async Task<IResult> ConfirmAsync(string holdId, string studentId)
    {
        var existing = await db.Holds.AsNoTracking().FirstOrDefaultAsync(h => h.Id == holdId);
        if (existing is null) return Error(404, "Hold not found");
        if (existing.StudentId != studentId) return Error(403, "This hold belongs to another student");

        await using var tx = await db.Database.BeginTransactionAsync();
        await LockClassAsync(existing.ClassId);

        // Re-read after taking the lock so we see the latest committed state.
        var hold = await db.Holds.FirstAsync(h => h.Id == holdId);
        var now = DateTime.UtcNow;

        if (hold.Status == HoldStatus.Confirmed)
            return Results.Ok(new HoldStatusDto(hold.Id, HoldStatus.Confirmed)); // idempotent

        if (hold.Status == HoldStatus.Held && hold.ExpiresAt <= now)
        {
            hold.Status = HoldStatus.Expired;
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return Error(409, "Hold has expired");
        }

        if (hold.Status != HoldStatus.Held)
            return Error(409, $"Hold is {hold.Status} and cannot be confirmed");

        hold.Status = HoldStatus.Confirmed;
        hold.ConfirmedAt = now;
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new HoldStatusDto(hold.Id, HoldStatus.Confirmed));
    }

    public async Task<IResult> CancelAsync(string holdId, string studentId)
    {
        var existing = await db.Holds.AsNoTracking().FirstOrDefaultAsync(h => h.Id == holdId);
        if (existing is null) return Error(404, "Hold not found");
        if (existing.StudentId != studentId) return Error(403, "This hold belongs to another student");

        await using var tx = await db.Database.BeginTransactionAsync();
        await LockClassAsync(existing.ClassId);

        var hold = await db.Holds.FirstAsync(h => h.Id == holdId);
        var now = DateTime.UtcNow;

        if (hold.Status == HoldStatus.Cancelled) return Results.NoContent(); // idempotent
        if (hold.Status == HoldStatus.Confirmed) return Error(409, "A confirmed application cannot be cancelled");

        if (hold.Status == HoldStatus.Expired || hold.ExpiresAt <= now)
        {
            hold.Status = HoldStatus.Expired;
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return Error(409, "Hold has already expired");
        }

        hold.Status = HoldStatus.Cancelled;
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.NoContent();
    }

    public async Task<IResult> MyHoldsAsync(string studentId)
    {
        var now = DateTime.UtcNow;
        var rows = await db.Holds.AsNoTracking()
            .Where(h => h.StudentId == studentId)
            .Join(db.Classes, h => h.ClassId, c => c.Id, (h, c) => new { Hold = h, ClassName = c.Name })
            .OrderByDescending(x => x.Hold.CreatedAt)
            .ToListAsync();

        return Results.Ok(rows.Select(x =>
            new MyHoldDto(x.Hold.Id, x.Hold.ClassId, x.ClassName, x.Hold.EffectiveStatus(now), x.Hold.ExpiresAt)));
    }
}
