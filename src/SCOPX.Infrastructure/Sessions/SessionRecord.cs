namespace SCOPX.Infrastructure.Sessions;


public sealed record SessionRecord(Guid Id, Guid UserId, DateTimeOffset ExpiresAt, DateTimeOffset? RevokedAt);
