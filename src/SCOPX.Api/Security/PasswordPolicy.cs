namespace SCOPX.Api.Security;

public static class PasswordPolicy
{
    public const int MinimumLength = 12;
    public const int MaximumLength = 128;

    public static bool IsAcceptable(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinimumLength || password.Length > MaximumLength)
            return false;
        // Require variety, but do not force special characters which encourage predictable patterns.
        return password.Any(char.IsLower)
            && password.Any(char.IsUpper)
            && password.Any(char.IsDigit);
    }
}
