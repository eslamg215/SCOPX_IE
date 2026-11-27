namespace SCOPX.Infrastructure.Procedures;
public interface IProcedureRepository
{
 Task<IReadOnlyList<ProcedureRecord>> ListForPatientAsync(Guid hospitalId, Guid actorUserId, Guid patientId, CancellationToken ct = default);
 Task<ProcedureRecord?> GetAsync(Guid hospitalId, Guid actorUserId, Guid procedureId, CancellationToken ct = default);
 Task<ProcedureRecord?> CreateAsync(Guid hospitalId, Guid actorUserId, ProcedureCreateRequest input, CancellationToken ct = default);
 Task<bool> StartAsync(Guid hospitalId, Guid actorUserId, Guid procedureId, DateTimeOffset startedAt, CancellationToken ct = default);
 Task<bool> CompleteAsync(Guid hospitalId, Guid actorUserId, Guid actorMembershipId, Guid procedureId, DateTimeOffset endedAt, string? findings, string? impression, CancellationToken ct = default);
}
