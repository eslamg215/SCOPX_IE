namespace SCOPX.Api.Security;

// Minimal password-hashing subject; never store or expose the password itself.
public sealed class AuthUser
{
    public string Id { get; init; } = string.Empty;
}
