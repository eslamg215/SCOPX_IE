namespace SCOPX.Infrastructure.Hospitals;

/// <summary>Active hospital membership and role codes for context selection. No clinical fields.</summary>
public sealed record HospitalMembershipRecord(Guid HospitalId, string HospitalName, string MembershipStatus, IReadOnlyList<string> RoleCodes);
