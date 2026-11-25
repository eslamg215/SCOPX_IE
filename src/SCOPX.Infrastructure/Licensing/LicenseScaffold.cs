namespace SCOPX.Infrastructure.Licensing;


/// <summary>Non-enforcing licensing placeholder. Do not use for access control or production licensing.</summary>
public enum LicenseStatus { Unconfigured, Trial, Active, Expired, Revoked, Invalid }


/// <summary>Describes a future signed license; this DTO is not cryptographically verified.</summary>
public sealed record LicenseDescriptor(
    string LicenseId,
    string TenantId,
    string Product,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    string SignatureBase64);


/// <summary>Fail-closed placeholder. Always reports Unconfigured until a verified provider is implemented.</summary>
public sealed class DisabledLicenseProvider
{
    public LicenseStatus Status => LicenseStatus.Unconfigured;
    public bool IsValid => false;
    public string Notice => "Licensing is not configured or validated. This scaffold must not grant access.";
}
