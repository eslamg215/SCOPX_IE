namespace SCOPX.Infrastructure.Patients;

/// <summary>API-safe patient record. Notes are returned only by the detail query.</summary>
public sealed record PatientRecord(Guid Id, Guid HospitalId, string PatientCode, string FullName,
    DateOnly? DateOfBirth, string? SexAtBirth, string? ContactPhone, string? Notes,
    string Status, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset? ArchivedAt);
public sealed record PatientListItem(Guid Id, string PatientCode, string FullName, DateOnly? DateOfBirth,
    string? SexAtBirth, string? ContactPhone, string Status, DateTimeOffset CreatedAt);
public sealed record PatientCreateRequest(string PatientCode, string FullName, DateOnly? DateOfBirth,
    string? SexAtBirth, string? ContactPhone, string? Notes);
public sealed record PatientUpdateRequest(string FullName, DateOnly? DateOfBirth, string? SexAtBirth,
    string? ContactPhone, string? Notes);
