namespace SchoolAdmission.Api;

public record CreateClassRequest(string? Name, int TotalSeats);

public record ClassDto(string Id, string Name, int TotalSeats, int HeldSeats, int ConfirmedSeats, int AvailableSeats);

public record HoldCreatedDto(string HoldId, string ClassId, string StudentId, string Status, DateTime ExpiresAt);

public record HoldDto(string HoldId, string ClassId, string StudentId, string Status, DateTime ExpiresAt);

public record HoldStatusDto(string HoldId, string Status);

public record MyHoldDto(string HoldId, string ClassId, string ClassName, string Status, DateTime ExpiresAt);

public record HoldOptions(int DurationSeconds);

public record CreateApplicationRequest(string? GuardianName, string? StudentName, string? Nid, string? ClassId);

public record ApplicationDto(string Id, string GuardianName, string StudentName, string Nid, string StudentId,
    string ClassId, string ClassName, string Status, string? HoldId, string? HoldStatus, DateTime? ExpiresAt, DateTime CreatedAt);
