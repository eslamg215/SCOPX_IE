namespace SCOPX.Api.Security;

/// <summary>Deterministic lockout policy helper. Persistence and enforcement must be handled atomically by the auth service.</summary>
public static class LoginLockoutPolicy
{
    public const int MaxFailedAttemptsBeforeLockout = 5;
    public static TimeSpan LockoutFor(int failedAttempts)
    {
        if (failedAttempts < MaxFailedAttemptsBeforeLockout) return TimeSpan.Zero;
        // Escalating lockouts: 5-9 failures = 15 min, 10-14 = 1 hour, 15+ = 24 hours.
        var band = (failedAttempts - MaxFailedAttemptsBeforeLockout) / 5;
        return band switch
        {
            0 => TimeSpan.FromMinutes(15),
            1 => TimeSpan.FromHours(1),
            _ => TimeSpan.FromHours(24)
        };
    }
    public static bool IsLocked(DateTimeOffset? lockoutUntil, DateTimeOffset nowUtc) =>
        lockoutUntil.HasValue && lockoutUntil.Value > nowUtc;
}
