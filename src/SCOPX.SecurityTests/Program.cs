using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using SCOPX.Api.Reports;
using SCOPX.Api.Media;
using SCOPX.Api.Authentication;

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException("FAIL: " + message);
    Console.WriteLine("PASS: " + message);
}


static async Task RequireThrowsAsync<T>(Func<Task> action, string message) where T : Exception
{
    try { await action(); }
    catch (T) { Console.WriteLine("PASS: " + message); return; }
    throw new InvalidOperationException("FAIL: " + message);
}

static async Task<string> ReadNullTerminatedAsync(Stream stream)
{
    using var buffer = new MemoryStream();
    var oneByte = new byte[1];
    while (true)
    {
        var read = await stream.ReadAsync(oneByte);
        if (read == 0) throw new EndOfStreamException();
        if (oneByte[0] == 0) return Encoding.ASCII.GetString(buffer.ToArray());
        buffer.WriteByte(oneByte[0]);
    }
}

static async Task RunClamAvFixtureAsync(string response, bool expectedClean, string label)
{
    using var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    var payload = Enumerable.Range(0, 100_000).Select(i => (byte)(i % 251)).ToArray();

    var server = Task.Run(async () =>
    {
        using var client = await listener.AcceptTcpClientAsync();
        await using var stream = client.GetStream();
        var command = await ReadNullTerminatedAsync(stream);
        if (command != "zINSTREAM") throw new InvalidOperationException("Unexpected ClamAV command.");

        var header = new byte[4];
        var received = 0;
        while (true)
        {
            await stream.ReadExactlyAsync(header);
            var length = BinaryPrimitives.ReadInt32BigEndian(header);
            if (length == 0) break;
            var chunk = new byte[length];
            await stream.ReadExactlyAsync(chunk);
            received += length;
        }

        if (received != payload.Length) throw new InvalidOperationException("ClamAV stream length mismatch.");
        await stream.WriteAsync(Encoding.UTF8.GetBytes(response + "\0"));
    });

    var config = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MediaScanning:ClamAvHost"] = IPAddress.Loopback.ToString(),
            ["MediaScanning:ClamAvPort"] = port.ToString(),
            ["MediaScanning:TimeoutSeconds"] = "5"
        })
        .Build();

    var scanner = new ClamAvMediaMalwareScanner(config);
    var actualClean = await scanner.IsCleanAsync(payload, CancellationToken.None);
    await server;
    listener.Stop();
    Require(actualClean == expectedClean, label);
}

static void RequireThrows<T>(Action action, string message) where T : Exception
{
    try { action(); }
    catch (T) { Console.WriteLine("PASS: " + message); return; }
    throw new InvalidOperationException("FAIL: " + message);
}

// Authentication header parsing is tested without starting clinical API routes or using real identities.
Require(AuthHeaderReader.ReadBearer("") is null, "empty authorization header is rejected");
Require(AuthHeaderReader.ReadBearer("Basic dXNlcjpwYXNz") is null, "non-Bearer authorization schemes are rejected");
Require(AuthHeaderReader.ReadBearer("Bearer") is null, "Bearer scheme without a token is rejected");
Require(AuthHeaderReader.ReadBearer("Bearer " + new string('a', 29)) is null, "bearer tokens shorter than the minimum length are rejected");
Require(AuthHeaderReader.ReadBearer("Bearer " + new string('a', 30)) == new string('a', 30), "minimum-length opaque bearer token is accepted");
Require(AuthHeaderReader.ReadBearer("bEaReR " + new string('b', 64)) == new string('b', 64), "Bearer scheme matching is case-insensitive");
Require(AuthHeaderReader.ReadBearer("Bearer   " + new string('c', 64) + "  ") == new string('c', 64), "surrounding bearer-token whitespace is trimmed");
Require(AuthHeaderReader.ReadBearer("Bearer " + new string('d', 128)) == new string('d', 128), "maximum-length bearer token is accepted");
Require(AuthHeaderReader.ReadBearer("Bearer " + new string('e', 129)) is null, "bearer tokens longer than the maximum length are rejected");

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



var mediaKeyA = RandomNumberGenerator.GetBytes(32);
var mediaKeyB = RandomNumberGenerator.GetBytes(32);
var hospitalId = Guid.NewGuid();
var mediaId = Guid.NewGuid();
var mediaPlaintext = Enumerable.Range(0, 4096).Select(i => (byte)(i % 251)).ToArray();
var keyAConfig = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["MediaStorage:ActiveKeyId"] = "media-key-a",
        ["MediaStorage:Keys:media-key-a"] = Convert.ToBase64String(mediaKeyA)
    }).Build();
var keyAProtector = new MediaContentProtector(keyAConfig);
var encryptedWithA = keyAProtector.Encrypt(hospitalId, mediaId, mediaPlaintext);
Require(Encoding.ASCII.GetString(encryptedWithA, 0, 4) == "SCX2",
    "media encryption uses a versioned key-identified envelope");
Require(keyAProtector.Decrypt(hospitalId, mediaId, encryptedWithA).SequenceEqual(mediaPlaintext),
    "media AES-GCM encryption round-trips with the active key");

var rotationConfig = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["MediaStorage:ActiveKeyId"] = "media-key-b",
        ["MediaStorage:Keys:media-key-a"] = Convert.ToBase64String(mediaKeyA),
        ["MediaStorage:Keys:media-key-b"] = Convert.ToBase64String(mediaKeyB)
    }).Build();
var rotationProtector = new MediaContentProtector(rotationConfig);
Require(rotationProtector.Decrypt(hospitalId, mediaId, encryptedWithA).SequenceEqual(mediaPlaintext),
    "rotated keyring retains decryption access to existing media");
var encryptedWithB = rotationProtector.Encrypt(hospitalId, mediaId, mediaPlaintext);
Require(rotationProtector.Decrypt(hospitalId, mediaId, encryptedWithB).SequenceEqual(mediaPlaintext),
    "new media is encrypted with the newly active key");

var newKeyOnlyConfig = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["MediaStorage:ActiveKeyId"] = "media-key-b",
        ["MediaStorage:Keys:media-key-b"] = Convert.ToBase64String(mediaKeyB)
    }).Build();
var newKeyOnlyProtector = new MediaContentProtector(newKeyOnlyConfig);
RequireThrows<InvalidOperationException>(() => newKeyOnlyProtector.Decrypt(hospitalId, mediaId, encryptedWithA),
    "old media cannot be decrypted after its key is removed from the keyring");

var tamperedMedia = encryptedWithB.ToArray();
tamperedMedia[^1] ^= 0x01;
RequireThrows<CryptographicException>(() => rotationProtector.Decrypt(hospitalId, mediaId, tamperedMedia),
    "tampered media ciphertext is rejected");
RequireThrows<CryptographicException>(() => rotationProtector.Decrypt(Guid.NewGuid(), mediaId, encryptedWithB),
    "media ciphertext cannot be moved across hospital tenants");

CryptographicOperations.ZeroMemory(mediaKeyA);
CryptographicOperations.ZeroMemory(mediaKeyB);

await RunClamAvFixtureAsync("stream: OK", true, "ClamAV INSTREAM clean response is accepted");
await RunClamAvFixtureAsync("stream: Eicar-Test-Signature FOUND", false, "ClamAV INSTREAM infected response is rejected");
var noScannerConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
var unavailableScanner = new ClamAvMediaMalwareScanner(noScannerConfig);
await RequireThrowsAsync<InvalidOperationException>(
    async () => { await unavailableScanner.IsCleanAsync(new byte[] { 1 }, CancellationToken.None); },
    "media upload scanning fails closed when ClamAV is not configured");

Console.WriteLine("All SCOPX security tests passed.");
