namespace SchoolAdmission.Api.Data;

public static class HoldStatus
{
    public const string Held = "HELD";
    public const string Confirmed = "CONFIRMED";
    public const string Expired = "EXPIRED";
    public const string Cancelled = "CANCELLED";
}

public class SchoolClass
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int TotalSeats { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class Hold
{
    public string Id { get; set; } = "";
    public string ClassId { get; set; } = "";
    public string StudentId { get; set; } = "";
    public string Status { get; set; } = HoldStatus.Held;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }

    /// A HELD hold whose expiry time has passed is treated as EXPIRED even if the
    /// background job has not updated the row yet.
    public string EffectiveStatus(DateTime nowUtc) =>
        Status == HoldStatus.Held && ExpiresAt <= nowUtc ? HoldStatus.Expired : Status;
}

public static class AppStatus
{
    public const string Pending = "PENDING";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
}

/// A guardian's admission application. PENDING until an admin approves it; approval creates the seat hold.
public class Application
{
    public string Id { get; set; } = "";
    public string GuardianName { get; set; } = "";
    public string StudentName { get; set; } = "";
    public string Nid { get; set; } = "";
    public string StudentId { get; set; } = "";
    public string ClassId { get; set; } = "";
    public string Status { get; set; } = AppStatus.Pending;
    public string? HoldId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DecidedAt { get; set; }
}
