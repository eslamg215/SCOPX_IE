namespace SCOPX.Infrastructure.Users;


public interface IUserRepository
{
    Task<UserRecord?> FindByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);
    Task<UserRecord?> FindByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserRecord?> IncrementFailedLoginAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> RecordFailedLoginAsync(Guid userId, int failedCount, DateTimeOffset? lockoutUntil, CancellationToken cancellationToken = default);
    Task<bool> RecordSuccessfulLoginAsync(Guid userId, CancellationToken cancellationToken = default);
}
