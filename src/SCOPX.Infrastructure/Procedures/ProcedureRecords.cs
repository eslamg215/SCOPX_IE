namespace SCOPX.Infrastructure.Procedures;
public sealed record ProcedureRecord(Guid Id, Guid HospitalId, Guid PatientId, string ProcedureType, DateTimeOffset? ScheduledAt, DateTimeOffset? StartedAt, DateTimeOffset? EndedAt, string Status, string? RoomLabel, string? ClinicalIndication, string? Findings, string? Impression, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record ProcedureCreateRequest(Guid PatientId, string ProcedureType, DateTimeOffset? ScheduledAt, string? RoomLabel, string? ClinicalIndication);
public sealed record ProcedureClinicalUpdate(string? Findings, string? Impression);
public sealed record ProcedureStartRequest(DateTimeOffset? StartedAt);
public sealed record ProcedureCompleteRequest(DateTimeOffset? EndedAt, string? Findings, string? Impression);
