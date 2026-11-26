namespace SCOPX.Infrastructure.Reports;
public sealed record ProcedureReportRecord(Guid Id, Guid HospitalId, Guid ProcedureId, int Version, string Status, string Body, string BodySha256, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record ReportCreateRequest(Guid ProcedureId, string Body);
public sealed record ReportUpdateRequest(string Body, int ExpectedVersion);
