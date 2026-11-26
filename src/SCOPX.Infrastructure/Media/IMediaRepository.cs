namespace SCOPX.Infrastructure.Media;
public interface IMediaRepository
{
 Task<bool> ProcedureExistsAsync(Guid hospitalId, Guid procedureId, CancellationToken ct);
 Task<MediaAssetRecord?> CreateAsync(Guid actorUserId, MediaUploadMetadata metadata, CancellationToken ct);
 Task<IReadOnlyList<MediaAssetRecord>> ListForProcedureAsync(Guid hospitalId, Guid actorUserId, Guid procedureId, CancellationToken ct);
 Task<(MediaAssetRecord? Asset, string? StorageKey)> FindAsync(Guid hospitalId, Guid actorUserId, Guid mediaId, CancellationToken ct);
 Task RecordDownloadAsync(Guid hospitalId, Guid actorUserId, Guid mediaId, CancellationToken ct);
}
