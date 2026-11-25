namespace SCOPX.Infrastructure.Media;
public sealed record MediaAssetRecord(Guid Id, Guid ProcedureId, string MediaType, string FileName, string ContentType, long FileSizeBytes, string Sha256Hex, DateTimeOffset CreatedAt);
public sealed record MediaUploadMetadata(Guid HospitalId, Guid ProcedureId, Guid MediaId, string StorageKey, string MediaType, string FileName, string ContentType, long SizeBytes, string Sha256Hex);
