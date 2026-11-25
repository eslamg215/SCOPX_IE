namespace SCOPX.Infrastructure.Hospitals;


public interface IHospitalMembershipRepository
{
    Task<IReadOnlyList<HospitalMembershipRecord>> ListActiveForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<HospitalMembershipRecord?> FindActiveForUserAndHospitalAsync(Guid userId, Guid hospitalId, CancellationToken cancellationToken = default);
}
