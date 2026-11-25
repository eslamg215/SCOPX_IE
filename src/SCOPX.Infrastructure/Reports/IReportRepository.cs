namespace SCOPX.Infrastructure.Reports;
public interface IReportRepository
{
 Task<IReadOnlyList<ProcedureReportRecord>> ListForProcedureAsync(Guid hospitalId, Guid actorUserId, Guid procedureId, CancellationToken ct=default);
 Task<ProcedureReportRecord?> GetAsync(Guid hospitalId, Guid actorUserId, Guid reportId, CancellationToken ct=default);
 Task<ProcedureReportRecord?> CreateDraftAsync(Guid hospitalId, Guid actorUserId, Guid actorMembershipId, Guid procedureId, string body, CancellationToken ct=default);
 Task<bool> UpdateDraftAsync(Guid hospitalId, Guid actorUserId, Guid reportId, string body, CancellationToken ct=default);
 Task<bool> SubmitReviewAsync(Guid hospitalId, Guid actorUserId, Guid reportId, CancellationToken ct=default);
}
