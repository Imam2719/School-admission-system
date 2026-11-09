using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api;
using SchoolAdmission.Api.Data;

var builder = WebApplication.CreateBuilder(args);

// ---- Configuration (all from environment variables) ----
var connectionString =
    builder.Configuration["DB_CONNECTION_STRING"]
    ?? builder.Configuration["ConnectionStrings:Default"]
    ?? "Host=localhost;Port=5432;Database=school_admission;Username=postgres;Password=postgres";

var holdSeconds = int.TryParse(builder.Configuration["HOLD_DURATION_SECONDS"], out var s) && s > 0 ? s : 600;

// Default port 8080 when not told otherwise (Docker sets ASPNETCORE_URLS itself).
if (!builder.Environment.IsEnvironment("Testing") && string.IsNullOrEmpty(builder.Configuration["ASPNETCORE_URLS"]))
    builder.WebHost.UseUrls("http://localhost:8080");

builder.Services.AddDbContext<AppDb>(o => o.UseNpgsql(connectionString));
builder.Services.AddSingleton(new HoldOptions(holdSeconds));
builder.Services.AddScoped<AdmissionService>();
builder.Services.AddScoped<ApplicationService>();
builder.Services.AddHostedService<ExpiryWorker>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseExceptionHandler(a => a.Run(async ctx =>
{
    ctx.Response.StatusCode = 500;
    await ctx.Response.WriteAsJsonAsync(new { error = "Internal server error" });
}));
app.UseCors();

// ---- Create schema (retry while Postgres is starting) ----
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    for (var attempt = 1; ; attempt++)
    {
        try { db.Database.EnsureCreated(); break; }
        catch (Exception ex) when (attempt < 30)
        {
            app.Logger.LogWarning("Database not ready (attempt {Attempt}): {Message}", attempt, ex.Message);
            Thread.Sleep(2000);
        }
    }
}

IResult MissingStudent() =>
    Results.Json(new { error = "X-Student-Id header is required" }, statusCode: 400);

const string H = "X-Student-Id";

// ---- Classes ----
app.MapPost("/classes", (CreateClassRequest req, AdmissionService svc) => svc.CreateClassAsync(req));
app.MapGet("/classes", (AdmissionService svc) => svc.ListClassesAsync());
app.MapGet("/classes/{classId}", (string classId, AdmissionService svc) => svc.GetClassAsync(classId));

// ---- Holds ----
app.MapPost("/classes/{classId}/holds",
    async (string classId, [FromHeader(Name = H)] string? studentId, AdmissionService svc) =>
        string.IsNullOrWhiteSpace(studentId) ? MissingStudent() : await svc.CreateHoldAsync(classId, studentId.Trim()));

app.MapGet("/holds/{holdId}", (string holdId, AdmissionService svc) => svc.GetHoldAsync(holdId));

app.MapPost("/holds/{holdId}/confirm",
    async (string holdId, [FromHeader(Name = H)] string? studentId, AdmissionService svc) =>
        string.IsNullOrWhiteSpace(studentId) ? MissingStudent() : await svc.ConfirmAsync(holdId, studentId.Trim()));

app.MapDelete("/holds/{holdId}",
    async (string holdId, [FromHeader(Name = H)] string? studentId, AdmissionService svc) =>
        string.IsNullOrWhiteSpace(studentId) ? MissingStudent() : await svc.CancelAsync(holdId, studentId.Trim()));

app.MapGet("/me/holds",
    async ([FromHeader(Name = H)] string? studentId, AdmissionService svc) =>
        string.IsNullOrWhiteSpace(studentId) ? MissingStudent() : await svc.MyHoldsAsync(studentId.Trim()));

// ---- Guardian application + admin approval (added on top of the 8 contract endpoints) ----
app.MapPost("/applications", (CreateApplicationRequest req, ApplicationService svc) => svc.CreateAsync(req));
app.MapGet("/applications", (string? nid, ApplicationService svc) => svc.ByNidAsync(nid));
app.MapGet("/admin/applications", (ApplicationService svc) => svc.AllAsync());
app.MapPost("/applications/{id}/approve", (string id, ApplicationService svc) => svc.ApproveAsync(id));
app.MapPost("/applications/{id}/reject", (string id, ApplicationService svc) => svc.RejectAsync(id));

app.Run();

public partial class Program { } // needed by the integration tests
