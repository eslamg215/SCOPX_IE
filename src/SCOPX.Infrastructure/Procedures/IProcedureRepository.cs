namespace SCOPX.Infrastructure.Procedures;
public interface IProcedureRepository
{
 Task<IReadOnlyList<ProcedureRecord>> ListForPatientAsync(Guid hospitalId, Guid patientId, CancellationToken ct = default);
 Task<ProcedureRecord?> GetAsync(Guid hospitalId, Guid procedureId, CancellationToken ct = default);
 Task<ProcedureRecord?> CreateAsync(Guid hospitalId, ProcedureCreateRequest input, CancellationToken ct = default);
 Task<bool> StartAsync(Guid hospitalId, Guid procedureId, DateTimeOffset startedAt, CancellationToken ct = default);
 Task<bool> CompleteAsync(Guid hospitalId, Guid procedureId, DateTimeOffset endedAt, string? findings, string? impression, CancellationToken ct = default);
}
