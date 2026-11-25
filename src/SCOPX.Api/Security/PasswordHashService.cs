using Microsoft.AspNetCore.Identity;

namespace SCOPX.Api.Security;


/// <summary>Uses ASP.NET Core's adaptive password hashing. The production user store and authentication
/// endpoints must not be enabled until database-backed lockout, auditing and tenant checks are ready.</summary>
public sealed class PasswordHashService(IPasswordHasher<AuthUser> hasher)
{
    public string Hash(AuthUser user, string password)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (!PasswordPolicy.IsAcceptable(password))
            throw new ArgumentException("Password does not meet the configured policy.", nameof(password));
        return hasher.HashPassword(user, password);
    }

    public PasswordVerificationResult Verify(AuthUser user, string storedHash, string suppliedPassword)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(storedHash);
        if (string.IsNullOrEmpty(suppliedPassword) || suppliedPassword.Length > PasswordPolicy.MaximumLength)
            return PasswordVerificationResult.Failed;
        return hasher.VerifyHashedPassword(user, storedHash, suppliedPassword);
    }
}
