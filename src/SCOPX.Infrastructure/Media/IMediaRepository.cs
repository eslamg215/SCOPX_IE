namespace SCOPX.Infrastructure.Media;
public interface IMediaRepository
{
 Task<bool> ProcedureExistsAsync(Guid hospitalId, Guid procedureId, CancellationToken ct);
 Task<MediaAssetRecord?> CreateAsync(Guid actorUserId, MediaUploadMetadata metadata, CancellationToken ct);
 Task<IReadOnlyList<MediaAssetRecord>> ListForProcedureAsync(Guid hospitalId, Guid procedureId, CancellationToken ct);
 Task<(MediaAssetRecord? Asset, string? StorageKey)> FindAsync(Guid hospitalId, Guid mediaId, CancellationToken ct);
}
