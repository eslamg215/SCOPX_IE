namespace SCOPX.Infrastructure.Sessions;


public interface ISessionRepository
{
    Task CreateAsync(Guid userId, string tokenHash, DateTimeOffset expiresAt, CancellationToken cancellationToken = default);
    Task<SessionRecord?> FindActiveByTokenHashAsync(string tokenHash, DateTimeOffset nowUtc, CancellationToken cancellationToken = default);
    Task<bool> RevokeByTokenHashAsync(string tokenHash, DateTimeOffset nowUtc, CancellationToken cancellationToken = default);
}
