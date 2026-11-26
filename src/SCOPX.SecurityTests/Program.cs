using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using SCOPX.Api.Reports;

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException("FAIL: " + message);
    Console.WriteLine("PASS: " + message);
}

static void RequireThrows<T>(Action action, string message) where T : Exception
{
    try { action(); }
    catch (T) { Console.WriteLine("PASS: " + message); return; }
    throw new InvalidOperationException("FAIL: " + message);
}

using var rsa = RSA.Create(3072);
var privatePem = rsa.ExportPkcs8PrivateKeyPem();
var publicPem = rsa.ExportSubjectPublicKeyInfoPem();
var configuration = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Reports:SigningPrivateKeyPem"] = privatePem,
        ["Reports:SigningKeyId"] = "ci-test-key-2026-01"
    })
    .Build();

var service = new ReportSigningService(configuration);
var now = DateTimeOffset.UtcNow;
var input = new ReportSigningInput(
    Guid.NewGuid(), Guid.NewGuid(), 3, new string('a', 64),
    Guid.NewGuid(), now, now - TimeSpan.FromMinutes(1), "password");

var artifact = service.Sign(input);
Require(artifact.SignatureAlgorithm == ReportSigningService.Algorithm,
    "report signatures use the configured RSA-PSS-SHA256 algorithm");
Require(service.Verify(artifact.Payload, artifact.SignatureAlgorithm,
    artifact.SignatureValue, publicPem), "signature verifies against the matching public key");

var tamperedSignature = artifact.SignatureValue.ToArray();
tamperedSignature[0] ^= 0x01;
Require(!service.Verify(artifact.Payload, artifact.SignatureAlgorithm,
    tamperedSignature, publicPem), "tampered signature bytes are rejected");

var tamperedPayload = artifact.Payload with { DocumentSha256 = new string('b', 64) };
Require(!service.Verify(tamperedPayload, artifact.SignatureAlgorithm,
    artifact.SignatureValue, publicPem), "a changed document digest invalidates the signature");

using var otherRsa = RSA.Create(3072);
Require(!service.Verify(artifact.Payload, artifact.SignatureAlgorithm,
    artifact.SignatureValue, otherRsa.ExportSubjectPublicKeyInfoPem()),
    "signature does not verify under an unrelated public key");

RequireThrows<ArgumentException>(() => service.Sign(input with
{
    ReauthenticatedAt = now - TimeSpan.FromMinutes(6)
}), "re-authentication older than five minutes is rejected");

var missingKeyConfig = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>())
    .Build();
var missingKeyService = new ReportSigningService(missingKeyConfig);
RequireThrows<InvalidOperationException>(() => missingKeyService.Sign(input),
    "signing fails closed when no private key is configured");

using var weakRsa = RSA.Create(1024);
var weakConfig = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Reports:SigningPrivateKeyPem"] = weakRsa.ExportPkcs8PrivateKeyPem(),
        ["Reports:SigningKeyId"] = "weak-ci-key"
    })
    .Build();
var weakService = new ReportSigningService(weakConfig);
RequireThrows<InvalidOperationException>(() => weakService.Sign(input),
    "RSA keys smaller than 2048 bits are rejected");

Console.WriteLine("All report-signing cryptography tests passed.");
