using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using SCOPX.Infrastructure.Reports;

namespace SCOPX.Api.Reports;

public sealed record ReportSigningInput(
    Guid HospitalId,
    Guid ReportId,
    int ReportVersion,
    string DocumentSha256,
    Guid SignerMembershipId,
    DateTimeOffset SignedAt,
    DateTimeOffset ReauthenticatedAt,
    string AuthenticationMethod);

public sealed record ReportSignaturePayload(
    Guid HospitalId,
    Guid ReportId,
    int ReportVersion,
    string DocumentSha256,
    Guid SignerMembershipId,
    DateTimeOffset SignedAt,
    DateTimeOffset ReauthenticatedAt,
    string AuthenticationMethod,
    string CanonicalizationVersion,
    string SigningKeyId);

public sealed record ReportSignatureArtifact(
    ReportSignaturePayload Payload,
    string SignatureAlgorithm,
    byte[] SignatureValue);

/// <summary>
/// Creates and verifies server-issued RSA-PSS attestations over a canonical report identity.
/// The caller must perform recent user re-authentication and transactional DB/audit writes.
/// The private key must be supplied by a managed secret provider; this service does not
/// generate, persist, rotate, or export private keys.
/// </summary>
public sealed class ReportSigningService(IConfiguration configuration)
{
    public const string Algorithm = "RSA-PSS-SHA256";
    public const string CanonicalizationVersion = "SCOPX-REPORT-TEXT-V1";

    public ReportSignatureArtifact Sign(ReportSigningInput input)
    {
        ValidateInput(input);
        var keyId = configuration["Reports:SigningKeyId"];
        var privatePem = configuration["Reports:SigningPrivateKeyPem"];
        if (!IsSafeKeyId(keyId) || string.IsNullOrWhiteSpace(privatePem))
            throw new InvalidOperationException("Report signing key is not configured.");

        var payload = new ReportSignaturePayload(
            input.HospitalId, input.ReportId, input.ReportVersion,
            input.DocumentSha256, input.SignerMembershipId,
            input.SignedAt.ToUniversalTime(), input.ReauthenticatedAt.ToUniversalTime(),
            input.AuthenticationMethod, CanonicalizationVersion, keyId!);

        using var rsa = RSA.Create();
        try
        {
            rsa.ImportFromPem(privatePem);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException("Report signing private key PEM is invalid.", ex);
        }

        if (rsa.KeySize < 2048)
            throw new InvalidOperationException("Report signing RSA key must be at least 2048 bits.");

        var signature = rsa.SignData(CanonicalBytes(payload), HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        return new ReportSignatureArtifact(payload, Algorithm, signature);
    }

    public bool Verify(ReportSignaturePayload payload, string signatureAlgorithm,
        ReadOnlySpan<byte> signatureValue, string publicKeyPem)
    {
        ValidatePayload(payload);
        if (!string.Equals(signatureAlgorithm, Algorithm, StringComparison.Ordinal) ||
            signatureValue.Length is < 128 or > 8192 ||
            string.IsNullOrWhiteSpace(publicKeyPem))
            return false;

        using var rsa = RSA.Create();
        try
        {
            rsa.ImportFromPem(publicKeyPem);
        }
        catch (ArgumentException)
        {
            return false;
        }

        if (rsa.KeySize < 2048)
            return false;

        return rsa.VerifyData(CanonicalBytes(payload), signatureValue,
            HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
    }

    public static byte[] CanonicalBytes(ReportSignaturePayload payload)
    {
        ValidatePayload(payload);
        var fields = new[]
        {
            "SCOPX.I.E-REPORT-SIGNATURE-V1",
            payload.HospitalId.ToString("N"),
            payload.ReportId.ToString("N"),
            payload.ReportVersion.ToString(CultureInfo.InvariantCulture),
            payload.DocumentSha256,
            payload.SignerMembershipId.ToString("N"),
            payload.SignedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            payload.ReauthenticatedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            payload.AuthenticationMethod,
            payload.CanonicalizationVersion,
            payload.SigningKeyId
        };
        return Encoding.UTF8.GetBytes(string.Join("\n", fields));
    }

    private static void ValidateInput(ReportSigningInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.HospitalId == Guid.Empty || input.ReportId == Guid.Empty ||
            input.SignerMembershipId == Guid.Empty || input.ReportVersion < 1 ||
            input.DocumentSha256.Length != 64 ||
            input.DocumentSha256.Any(ch => !Uri.IsHexDigit(ch) || char.IsUpper(ch)) ||
            input.AuthenticationMethod is not ("password" or "mfa" or "hardware_key") ||
            input.ReauthenticatedAt > input.SignedAt ||
            input.SignedAt - input.ReauthenticatedAt > TimeSpan.FromMinutes(5))
            throw new ArgumentException("Report signing input is invalid.", nameof(input));
    }

    private static void ValidatePayload(ReportSignaturePayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (payload.HospitalId == Guid.Empty || payload.ReportId == Guid.Empty ||
            payload.SignerMembershipId == Guid.Empty || payload.ReportVersion < 1 ||
            payload.DocumentSha256.Length != 64 ||
            payload.DocumentSha256.Any(ch => !Uri.IsHexDigit(ch) || char.IsUpper(ch)) ||
            payload.AuthenticationMethod is not ("password" or "mfa" or "hardware_key") ||
            payload.CanonicalizationVersion != CanonicalizationVersion ||
            !IsSafeKeyId(payload.SigningKeyId) ||
            payload.ReauthenticatedAt > payload.SignedAt ||
            payload.SignedAt - payload.ReauthenticatedAt > TimeSpan.FromMinutes(5))
            throw new ArgumentException("Report signature payload is invalid.", nameof(payload));
    }

    private static bool IsSafeKeyId(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 100 &&
        value.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '.' or '_' or '-');
}
