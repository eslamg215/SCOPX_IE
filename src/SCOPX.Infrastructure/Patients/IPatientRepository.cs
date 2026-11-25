namespace SCOPX.Infrastructure.Patients;


public interface IPatientRepository
{
    Task<IReadOnlyList<PatientListItem>> SearchAsync(Guid hospitalId, string? query, int limit, int offset, CancellationToken ct = default);
    Task<PatientRecord?> GetAsync(Guid hospitalId, Guid patientId, CancellationToken ct = default);
    Task<PatientRecord?> CreateAsync(Guid hospitalId, PatientCreateRequest input, CancellationToken ct = default);
    Task<bool> UpdateAsync(Guid hospitalId, Guid patientId, PatientUpdateRequest input, CancellationToken ct = default);
    Task<bool> ArchiveAsync(Guid hospitalId, Guid patientId, CancellationToken ct = default);
}
