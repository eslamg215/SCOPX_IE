namespace SCOPX.Infrastructure.Reports;
public interface IReportRepository
{
 Task<IReadOnlyList<ProcedureReportRecord>> ListForProcedureAsync(Guid hospitalId, Guid actorUserId, Guid procedureId, CancellationToken ct=default);
 Task<ProcedureReportRecord?> GetAsync(Guid hospitalId, Guid actorUserId, Guid reportId, CancellationToken ct=default);
 Task<ProcedureReportRecord?> CreateDraftAsync(Guid hospitalId, Guid actorUserId, Guid actorMembershipId, Guid procedureId, string body, CancellationToken ct=default);
 Task<bool> UpdateDraftAsync(Guid hospitalId, Guid actorUserId, Guid reportId, int expectedVersion, string body, CancellationToken ct=default);
 Task<bool> SubmitReviewAsync(Guid hospitalId, Guid actorUserId, Guid reportId, CancellationToken ct=default);
 Task<bool> SignAsync(Guid hospitalId, Guid actorUserId, Guid signerMembershipId, Guid reportId, int expectedVersion, string documentSha256, DateTimeOffset signedAt, DateTimeOffset reauthenticatedAt, string authenticationMethod, string canonicalizationVersion, string signatureAlgorithm, string signingKeyId, byte[] signatureValue, CancellationToken ct=default);
}
