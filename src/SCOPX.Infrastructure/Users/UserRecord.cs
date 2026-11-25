namespace SCOPX.Infrastructure.Users;


/// <summary>Minimal auth-facing user record. Never includes clinical or tenant data.</summary>
public sealed record UserRecord(
    Guid Id,
    string NormalizedEmail,
    string DisplayName,
    string Status,
    string? PasswordHash,
    int FailedLoginCount,
    DateTimeOffset? LockoutUntil,
    DateTimeOffset? LastLoginAt);
