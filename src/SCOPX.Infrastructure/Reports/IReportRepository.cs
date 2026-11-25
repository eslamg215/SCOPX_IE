namespace SCOPX.Infrastructure.Reports;
public interface IReportRepository
{
 Task<IReadOnlyList<ProcedureReportRecord>> ListForProcedureAsync(Guid hospitalId, Guid procedureId, CancellationToken ct=default);
 Task<ProcedureReportRecord?> GetAsync(Guid hospitalId, Guid reportId, CancellationToken ct=default);
 Task<ProcedureReportRecord?> CreateDraftAsync(Guid hospitalId, Guid procedureId, string body, CancellationToken ct=default);
 Task<bool> UpdateDraftAsync(Guid hospitalId, Guid reportId, string body, CancellationToken ct=default);
 Task<bool> SubmitReviewAsync(Guid hospitalId, Guid reportId, CancellationToken ct=default);
}
