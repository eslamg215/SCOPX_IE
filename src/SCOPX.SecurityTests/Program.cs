using Npgsql;
using NpgsqlTypes;
using Microsoft.Extensions.Logging;
using SCOPX.Infrastructure.Hospitals;
using SCOPX.Infrastructure.Sessions;
using SCOPX.Infrastructure.Users;
using SCOPX.Api.Tenancy;
using SCOPX.Api.Security;
using System.Threading.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using System.Text.Json;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using SCOPX.Api.Reports;
using SCOPX.Api.Media;
using SCOPX.Api.Authentication;
using SCOPX.Workstation.Recording;
using SCOPX.Workstation.Devices;

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

Require(LoginLockoutPolicy.LockoutFor(4) == TimeSpan.Zero, "fewer than five failed logins do not trigger account lockout");
Require(LoginLockoutPolicy.LockoutFor(5) == TimeSpan.FromMinutes(15), "five failed logins trigger a 15-minute lockout");
Require(LoginLockoutPolicy.LockoutFor(9) == TimeSpan.FromMinutes(15), "failed-login lockout remains 15 minutes through attempt nine");
Require(LoginLockoutPolicy.LockoutFor(10) == TimeSpan.FromHours(1), "ten failed logins trigger a one-hour lockout");
Require(LoginLockoutPolicy.LockoutFor(14) == TimeSpan.FromHours(1), "failed-login lockout remains one hour through attempt fourteen");
Require(LoginLockoutPolicy.LockoutFor(15) == TimeSpan.FromHours(24), "fifteen failed logins trigger a 24-hour lockout");
Require(LoginLockoutPolicy.IsLocked(DateTimeOffset.UtcNow.AddSeconds(1), DateTimeOffset.UtcNow), "future lockout expiry is enforced");
Require(!LoginLockoutPolicy.IsLocked(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow), "lockout expires at its exact expiry timestamp");
Require(PasswordPolicy.IsAcceptable("ScopxTestPass2026"), "password policy accepts a compliant synthetic password");
Require(!PasswordPolicy.IsAcceptable("shortA1"), "password policy rejects passwords below the minimum length");
Require(!PasswordPolicy.IsAcceptable(new string('A', 129) + "a1"), "password policy rejects passwords above the maximum length");
Require(!PasswordPolicy.IsAcceptable("alllowercase123"), "password policy requires uppercase, lowercase, and numeric characters");

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

var jpegFixture = new byte[] { 0xFF, 0xD8, 0xFF, 0x00 };
var pngFixture = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
var webpFixture = Encoding.ASCII.GetBytes("RIFF\0\0\0\0WEBP");
var mp4Fixture = new byte[] { 0, 0, 0, 24, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'i', (byte)'s', (byte)'o', (byte)'m' };
var webmFixture = new byte[] { 0x1A, 0x45, 0xDF, 0xA3, 0x00 };
Require(MediaFileSignatureValidator.Matches(".jpg", jpegFixture, out var jpegType, out var jpegContent) && jpegType == "image" && jpegContent == "image/jpeg",
    "JPEG media signature is accepted and assigned a server-derived content type");
Require(MediaFileSignatureValidator.Matches(".png", pngFixture, out _, out var pngContent) && pngContent == "image/png",
    "PNG media signature is accepted");
Require(MediaFileSignatureValidator.Matches(".webp", webpFixture, out _, out var webpContent) && webpContent == "image/webp",
    "WebP media signature is accepted");
Require(MediaFileSignatureValidator.Matches(".mp4", mp4Fixture, out var mp4Type, out var mp4Content) && mp4Type == "video" && mp4Content == "video/mp4",
    "MP4 media signature is accepted");
Require(MediaFileSignatureValidator.Matches(".webm", webmFixture, out _, out var webmContent) && webmContent == "video/webm",
    "WebM media signature is accepted");
Require(!MediaFileSignatureValidator.Matches(".jpg", pngFixture, out _, out _),
    "media extension and content signature mismatch is rejected");
Require(!MediaFileSignatureValidator.Matches(".exe", jpegFixture, out _, out _),
    "unsupported media extension is rejected");

Require(RecordingLifecyclePolicy.IsAllowed(RecordingLifecycleState.Idle, RecordingLifecycleState.Preparing),
    "recording lifecycle allows idle to preparing");
Require(RecordingLifecyclePolicy.IsAllowed(RecordingLifecycleState.Preparing, RecordingLifecycleState.Recording),
    "recording lifecycle allows preparation to recording");
Require(RecordingLifecyclePolicy.IsAllowed(RecordingLifecycleState.Recording, RecordingLifecycleState.Stopping),
    "recording lifecycle allows recording to stopping");
Require(RecordingLifecyclePolicy.IsAllowed(RecordingLifecycleState.Stopping, RecordingLifecycleState.Finalized),
    "recording lifecycle allows stopping to finalized");
Require(RecordingLifecyclePolicy.IsAllowed(RecordingLifecycleState.Recording, RecordingLifecycleState.Error),
    "recording lifecycle allows recording failure");
Require(RecordingLifecyclePolicy.IsAllowed(RecordingLifecycleState.Error, RecordingLifecycleState.Idle),
    "recording lifecycle requires error reset to idle");
Require(!RecordingLifecyclePolicy.IsAllowed(RecordingLifecycleState.Idle, RecordingLifecycleState.Recording),
    "recording lifecycle rejects starting without preparation");
Require(!RecordingLifecyclePolicy.IsAllowed(RecordingLifecycleState.Recording, RecordingLifecycleState.Finalized),
    "recording lifecycle rejects finalization without stopping");
Require(!RecordingLifecyclePolicy.IsAllowed(RecordingLifecycleState.Error, RecordingLifecycleState.Finalized),
    "recording lifecycle rejects finalizing a failed recording");
Require(RecordingLifecyclePolicy.Transition(RecordingLifecycleState.Stopping, RecordingLifecycleState.Finalized) == RecordingLifecycleState.Finalized,
    "recording lifecycle transition returns the validated next state");
RequireThrows<InvalidOperationException>(() => RecordingLifecyclePolicy.Transition(RecordingLifecycleState.Idle, RecordingLifecycleState.Finalized),
    "recording lifecycle throws on an invalid transition");

// Step 71: deterministic simulator adapter contract checks. This adapter emits no media.
await using (var simulator = new SimulatorCaptureAdapter())
{
    var initialStatus = await simulator.GetStatusAsync();
    Require(initialStatus.State == CaptureState.Ready
        && initialStatus.Message!.Contains("no real capture", StringComparison.OrdinalIgnoreCase),
        "capture simulator starts ready and explicitly declares that it produces no real video");

    await simulator.StartAsync(new CaptureStartOptions(1920, 1080, 30));
    var activeStatus = await simulator.GetStatusAsync();
    Require(activeStatus.State == CaptureState.Capturing,
        "capture simulator transitions to capturing after a valid start request");

    await RequireThrowsAsync<InvalidOperationException>(
        () => simulator.StartAsync(new CaptureStartOptions(1280, 720, 30)),
        "capture simulator rejects a duplicate start request");

    await simulator.StopAsync();
    var stoppedStatus = await simulator.GetStatusAsync();
    Require(stoppedStatus.State == CaptureState.Ready,
        "capture simulator returns to ready after stop");

    await RequireThrowsAsync<ArgumentOutOfRangeException>(
        () => simulator.StartAsync(new CaptureStartOptions(0, 1080, 30)),
        "capture simulator rejects invalid dimensions");

    using var cancelled = new CancellationTokenSource();
    cancelled.Cancel();
    await RequireThrowsAsync<OperationCanceledException>(
        () => simulator.StartAsync(new CaptureStartOptions(640, 480, 30), cancelled.Token),
        "capture simulator honors cancellation before starting");
    Require((await simulator.GetStatusAsync()).State == CaptureState.Ready,
        "cancelled simulator start leaves capture inactive");

    await simulator.DisposeAsync();
    await RequireThrowsAsync<ObjectDisposedException>(
        () => simulator.StartAsync(new CaptureStartOptions(640, 480, 30)),
        "disposed capture simulator cannot be restarted");
}

// Step 72: validate adapter-provided descriptors as untrusted display metadata.
var simulatorDescriptor = new SimulatorCaptureAdapter().Descriptor;
Require(CaptureDeviceDescriptorPolicy.IsValid(simulatorDescriptor, out _),
    "capture descriptor policy accepts the explicitly labelled no-video simulator");
var syntheticUsbDescriptor = new CaptureDeviceDescriptor(
    "usb:synthetic-device-01", "Synthetic USB Capture Adapter", CaptureTransport.Usb,
    "Synthetic Vendor", "Test Model", CaptureState.Ready);
Require(CaptureDeviceDescriptorPolicy.IsValid(syntheticUsbDescriptor, out _),
    "capture descriptor policy accepts bounded synthetic USB metadata");
Require(!CaptureDeviceDescriptorPolicy.IsValid(
    syntheticUsbDescriptor with { DisplayName = "  " }, out _),
    "capture descriptor policy rejects blank display names");
Require(!CaptureDeviceDescriptorPolicy.IsValid(
    syntheticUsbDescriptor with { DisplayName = "Unsafe\nDevice" }, out _),
    "capture descriptor policy rejects control characters in metadata");
Require(!CaptureDeviceDescriptorPolicy.IsValid(
    syntheticUsbDescriptor with { Transport = (CaptureTransport)999 }, out _),
    "capture descriptor policy rejects unknown transport enum values");
Require(!CaptureDeviceDescriptorPolicy.IsValid(
    syntheticUsbDescriptor with { DeviceId = "simulator://spoofed" }, out _),
    "physical capture descriptors cannot spoof the simulator identifier");
Require(!CaptureDeviceDescriptorPolicy.IsValid(
    simulatorDescriptor with { DisplayName = "SCOPX Capture Device" }, out _),
    "simulator transport must remain explicitly labelled No Video");
Require(!CaptureDeviceDescriptorPolicy.IsValid(
    syntheticUsbDescriptor with { CompatibilityNote = new string('x', 501) }, out _),
    "capture descriptor policy enforces compatibility-note length bounds");

// Step 73: bound stream negotiation options before adapter invocation.
Require(CaptureStartOptionsPolicy.IsValid(new CaptureStartOptions(1920, 1080, 30, "MJPEG")),
    "capture options policy accepts a supported synthetic 1080p MJPEG profile");
Require(CaptureStartOptionsPolicy.IsValid(new CaptureStartOptions(7680, 4320, 120, "NV12")),
    "capture options policy accepts the documented upper numeric bounds and supported pixel format");
Require(!CaptureStartOptionsPolicy.IsValid(new CaptureStartOptions(7681, 1080, 30)),
    "capture options policy rejects width above the configured maximum");
Require(!CaptureStartOptionsPolicy.IsValid(new CaptureStartOptions(1920, 4321, 30)),
    "capture options policy rejects height above the configured maximum");
Require(!CaptureStartOptionsPolicy.IsValid(new CaptureStartOptions(1920, 1080, 121)),
    "capture options policy rejects frame rates above the configured maximum");
Require(!CaptureStartOptionsPolicy.IsValid(new CaptureStartOptions(1920, 1080, 30, "RAW")),
    "capture options policy rejects unsupported pixel formats");
Require(!CaptureStartOptionsPolicy.IsValid(new CaptureStartOptions(1920, 1080, 30, " MJPEG ")),
    "capture options policy rejects padded pixel-format names");
Require(!CaptureStartOptionsPolicy.IsValid(new CaptureStartOptions(1920, 1080, 30, new string('A', 17))),
    "capture options policy rejects oversized pixel-format names");

// Step 74: use a validating decorator at the capture-provider boundary.
await using (var guardedCapture = new ValidatedCaptureAdapter(new SimulatorCaptureAdapter()))
{
    Require(guardedCapture.Descriptor.Transport == CaptureTransport.Simulator,
        "validated adapter exposes only the constructor-validated descriptor");
    Require((await guardedCapture.GetStatusAsync()).State == CaptureState.Ready,
        "validated adapter accepts a well-formed simulator status");
    await RequireThrowsAsync<ArgumentOutOfRangeException>(
        () => guardedCapture.StartAsync(new CaptureStartOptions(1920, 1080, 30, "RAW")),
        "validated adapter blocks unsupported stream options before forwarding");
    Require((await guardedCapture.GetStatusAsync()).State == CaptureState.Ready,
        "blocked adapter start leaves the underlying simulator inactive");
    await guardedCapture.StartAsync(new CaptureStartOptions(1280, 720, 30, "MJPEG"));
    Require((await guardedCapture.GetStatusAsync()).State == CaptureState.Capturing,
        "validated adapter forwards an accepted start request");
    await RequireThrowsAsync<InvalidOperationException>(
        () => guardedCapture.StartAsync(new CaptureStartOptions(1280, 720, 30, "MJPEG")),
        "validated adapter blocks a second start while the device is already capturing");
    Require((await guardedCapture.GetStatusAsync()).State == CaptureState.Capturing,
        "rejected duplicate start leaves the active capture state unchanged");
    await guardedCapture.StopAsync();
    Require((await guardedCapture.GetStatusAsync()).State == CaptureState.Ready,
        "validated adapter forwards stop and observes the ready state");
    await guardedCapture.StopAsync();
    Require((await guardedCapture.GetStatusAsync()).State == CaptureState.Ready,
        "validated adapter treats stop while already Ready as an idempotent no-op");
    await guardedCapture.DisposeAsync();
    await RequireThrowsAsync<ObjectDisposedException>(
        () => guardedCapture.GetStatusAsync(),
        "validated adapter rejects status access after disposal");
}
await using (var optionsSimulator = new SimulatorCaptureAdapter())
{
    await RequireThrowsAsync<ArgumentOutOfRangeException>(
        () => optionsSimulator.StartAsync(new CaptureStartOptions(1920, 1080, 30, "RAW")),
        "simulator refuses unsupported pixel formats before entering capture state");
    Require((await optionsSimulator.GetStatusAsync()).State == CaptureState.Ready,
        "rejected capture options leave the simulator ready and inactive");
}

CryptographicOperations.ZeroMemory(mediaKeyA);
CryptographicOperations.ZeroMemory(mediaKeyB);

// Step 75: capture status validity and fault-state start gate.
Require(CaptureStatusPolicy.IsValid(new CaptureStatus(CaptureState.Faulted, "synthetic adapter fault", DateTimeOffset.UtcNow)),
    "capture status policy accepts a well-formed fault status for diagnostics");
Require(!CaptureStatusPolicy.IsValid(new CaptureStatus((CaptureState)999, "bad state", DateTimeOffset.UtcNow)),
    "capture status policy rejects unknown state enum values");
Require(!CaptureStatusPolicy.IsValid(new CaptureStatus(CaptureState.Ready, null, default)),
    "capture status policy rejects a missing update timestamp");
Require(!CaptureStatusPolicy.IsValid(new CaptureStatus(CaptureState.Ready, new string('x', 501), DateTimeOffset.UtcNow)),
    "capture status policy rejects oversized status messages");
Require(!CaptureStatusPolicy.IsValid(new CaptureStatus(CaptureState.Ready, "bad\nmessage", DateTimeOffset.UtcNow)),
    "capture status policy rejects control characters in status messages");
var faultedAdapter = new FaultedCaptureAdapter();
await using (var guardedFaultedAdapter = new ValidatedCaptureAdapter(faultedAdapter))
{
    await RequireThrowsAsync<InvalidOperationException>(
        () => guardedFaultedAdapter.StartAsync(new CaptureStartOptions(640, 480, 30)),
        "validated adapter refuses to start when the provider reports Faulted");
    Require(faultedAdapter.StartCalls == 0,
        "faulted adapter start request is blocked before reaching the provider");
    await guardedFaultedAdapter.StopAsync();
    Require(faultedAdapter.StopCalls == 1,
        "validated adapter forwards stop as a cleanup attempt when provider is Faulted");
}
var disconnectedAdapter = new FaultedCaptureAdapter { ReportedState = CaptureState.Disconnected };
await using (var guardedDisconnectedAdapter = new ValidatedCaptureAdapter(disconnectedAdapter))
{
    await RequireThrowsAsync<InvalidOperationException>(
        () => guardedDisconnectedAdapter.StopAsync(),
        "validated adapter refuses to stop a disconnected provider");
    Require(disconnectedAdapter.StopCalls == 0,
        "disconnected provider is not called for stop");
}

await RunClamAvFixtureAsync("stream: OK", true, "ClamAV INSTREAM clean response is accepted");
await RunClamAvFixtureAsync("stream: Eicar-Test-Signature FOUND", false, "ClamAV INSTREAM infected response is rejected");
var noScannerConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
var unavailableScanner = new ClamAvMediaMalwareScanner(noScannerConfig);
await RequireThrowsAsync<InvalidOperationException>(
    async () => { await unavailableScanner.IsCleanAsync(new byte[] { 1 }, CancellationToken.None); },
    "media upload scanning fails closed when ClamAV is not configured");

await RunAuthHttpIntegrationTestsAsync();
await RunLoginRateLimitHttpTestAsync();
await RunPostgresHttpIntegrationTestsAsync();


static async Task RunAuthHttpIntegrationTestsAsync()
{
    var invalidCorsConfig = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Cors:AllowedOrigins:0"] = "*" })
        .Build();
    try
    {
        new ServiceCollection().AddScopxCors(invalidCorsConfig);
        throw new InvalidOperationException("FAIL: wildcard CORS origin configuration should be rejected.");
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("Cors:AllowedOrigins", StringComparison.Ordinal))
    {
        Console.WriteLine("PASS: CORS configuration rejects wildcard origins");
    }
    foreach (var invalidOrigin in new[] { "http://evil.example", "https://app.scopx.test/path", "https://user:pass@app.scopx.test" })
    {
        var invalidConfig = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Cors:AllowedOrigins:0"] = invalidOrigin })
            .Build();
        try
        {
            new ServiceCollection().AddScopxCors(invalidConfig);
            throw new InvalidOperationException("FAIL: invalid CORS origin configuration should be rejected.");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Cors:AllowedOrigins", StringComparison.Ordinal))
        {
            Console.WriteLine("PASS: CORS configuration rejects unsafe origin " + invalidOrigin);
        }
    }
    var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<AuthUser>();
    var userId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    var disabledId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    var lockedId = Guid.Parse("10000000-0000-0000-0000-000000000003");
    var hospitalA = Guid.Parse("20000000-0000-0000-0000-000000000001");
    var hospitalB = Guid.Parse("20000000-0000-0000-0000-000000000002");
    const string password = "ScopxTestPass2026";
    UserRecord MakeUser(Guid id, string email, string status, int failures = 0, DateTimeOffset? lockout = null) =>
        new(id, email, "Synthetic " + email, status,
            hasher.HashPassword(new AuthUser { Id = id.ToString("N") }, password), failures, lockout, null);

    var users = new FakeUserRepository([
        MakeUser(userId, "ACTIVE@EXAMPLE.INVALID", "active"),
        MakeUser(disabledId, "DISABLED@EXAMPLE.INVALID", "disabled"),
        MakeUser(lockedId, "LOCKED@EXAMPLE.INVALID", "active", 5, DateTimeOffset.UtcNow.AddMinutes(10))
    ]);
    var sessions = new FakeSessionRepository();
    var memberships = new FakeHospitalMembershipRepository(userId, [
        new HospitalMembershipRecord(hospitalA, "Synthetic Hospital A", "active",
            Guid.Parse("30000000-0000-0000-0000-000000000001"), ["doctor"])
    ]);

    var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Cors:AllowedOrigins:0"] = "https://app.scopx.test"
    });
    builder.Logging.ClearProviders();
    builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
    builder.Services.AddSingleton<IUserRepository>(users);
    builder.Services.AddSingleton<ISessionRepository>(sessions);
    builder.Services.AddSingleton<IHospitalMembershipRepository>(memberships);
    builder.Services.AddScoped<Microsoft.AspNetCore.Identity.IPasswordHasher<AuthUser>, Microsoft.AspNetCore.Identity.PasswordHasher<AuthUser>>();
    builder.Services.AddScoped<PasswordHashService>();
    builder.Services.AddScopxRateLimiting();
    builder.Services.AddScopxCors(builder.Configuration);

    await using var app = builder.Build();
    app.UseScopxExceptionHandling();
    app.UseScopxSecurityHeaders();
    app.UseCors("scopx");
    app.UseRateLimiter();
    app.MapAuthEndpoints();
    app.MapHospitalContextEndpoints();
    app.MapGet("/_test/throw", () => Task.FromException<IResult>(new InvalidOperationException("synthetic-secret-exception-detail")));
    await app.StartAsync();
    try
    {
        var server = app.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>();
        var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient { BaseAddress = new Uri(address) };

        static void Check(HttpResponseMessage response, HttpStatusCode expected, string label)
        {
            if (response.StatusCode != expected)
                throw new InvalidOperationException($"FAIL: {label}; expected {(int)expected}, got {(int)response.StatusCode}.");
            Console.WriteLine("PASS: " + label);
        }

        using (var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me"))
        {
            request.Headers.Add("Origin", "https://app.scopx.test");
            using var response = await client.SendAsync(request);
            Check(response, HttpStatusCode.Unauthorized, "Allowed CORS origin still requires authentication");
            Require(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var allowedOrigin)
                && allowedOrigin.Single() == "https://app.scopx.test",
                "CORS returns the exact configured origin");
            Require(!response.Headers.Contains("Access-Control-Allow-Credentials"),
                "CORS does not enable credentialed cross-origin requests");
            Require(response.Headers.TryGetValues("Access-Control-Expose-Headers", out var exposed)
                && exposed.Single().Contains("X-Correlation-ID", StringComparison.OrdinalIgnoreCase),
                "CORS exposes only the correlation identifier needed by browser clients");
        }
        using (var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me"))
        {
            request.Headers.Add("Origin", "https://evil.example");
            using var response = await client.SendAsync(request);
            Check(response, HttpStatusCode.Unauthorized, "Disallowed CORS origin remains unauthenticated");
            Require(!response.Headers.Contains("Access-Control-Allow-Origin"),
                "CORS does not expose responses to an unlisted origin");
        }
        using (var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/me"))
        {
            request.Headers.Add("Origin", "https://app.scopx.test");
            request.Headers.Add("Access-Control-Request-Method", "GET");
            request.Headers.Add("Access-Control-Request-Headers", "authorization,x-hospital-id");
            using var response = await client.SendAsync(request);
            Check(response, HttpStatusCode.NoContent, "CORS accepts a valid preflight for the configured origin");
            Require(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var origin)
                && origin.Single() == "https://app.scopx.test",
                "CORS preflight echoes only the configured origin");
            Require(!response.Headers.Contains("Access-Control-Allow-Credentials"),
                "CORS preflight does not permit credentialed browser requests");
            Require(response.Headers.TryGetValues("Access-Control-Allow-Methods", out var methods)
                && methods.Single().Contains("GET", StringComparison.OrdinalIgnoreCase),
                "CORS preflight allows the requested safe HTTP method");
            Require(response.Headers.TryGetValues("Access-Control-Allow-Headers", out var headers)
                && headers.Single().Contains("authorization", StringComparison.OrdinalIgnoreCase)
                && headers.Single().Contains("x-hospital-id", StringComparison.OrdinalIgnoreCase),
                "CORS preflight permits only the configured authentication and tenant headers");
        }

        using (var malformed = await client.PostAsync("/api/v1/auth/login",
            new StringContent("{", Encoding.UTF8, "application/json")))
            Check(malformed, HttpStatusCode.BadRequest, "HTTP login rejects malformed JSON");
        using (var wrongContentType = await client.PostAsync("/api/v1/auth/login",
            new StringContent("{\"email\":\"active@example.invalid\"}", Encoding.UTF8, "text/plain")))
            Check(wrongContentType, HttpStatusCode.UnsupportedMediaType, "HTTP login rejects unsupported content types");
        var oversizedLoginBody = "{\"email\":\"" + new string('a', 17 * 1024) + "\",\"password\":\"ScopxTestPass2026\"}";
        using (var oversized = await client.PostAsync("/api/v1/auth/login",
            new StringContent(oversizedLoginBody, Encoding.UTF8, "application/json")))
            Check(oversized, HttpStatusCode.RequestEntityTooLarge, "HTTP login rejects request bodies larger than 16 KiB");

        using var login = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "active@example.invalid", password });
        Check(login, HttpStatusCode.OK, "HTTP login accepts a valid synthetic account");
        Require(login.Headers.TryGetValues("X-Content-Type-Options", out var nosniff) && nosniff.Single() == "nosniff",
            "HTTP responses include X-Content-Type-Options nosniff");
        Require(login.Headers.TryGetValues("X-Frame-Options", out var frame) && frame.Single() == "DENY",
            "HTTP responses deny framing");
        Require(login.Headers.TryGetValues("Referrer-Policy", out var referrer) && referrer.Single() == "no-referrer",
            "HTTP responses use a no-referrer policy");
        Require(login.Headers.CacheControl?.NoStore == true,
            "HTTP responses disable caching of authentication data");
        Require(login.Headers.TryGetValues("Content-Security-Policy", out var csp)
            && csp.Single().Contains("default-src 'none'", StringComparison.Ordinal),
            "HTTP responses include a restrictive content security policy");
        using var loginJson = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var token = loginJson.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Login did not return access_token.");
        Require(token.Length >= 40, "HTTP login returns a high-entropy bearer token");

        using (var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me"))
        {
            request.Headers.Add("X-Correlation-ID", "safe_Correlation-55");
            using var response = await client.SendAsync(request);
            Check(response, HttpStatusCode.Unauthorized, "Correlation-ID test request remains unauthenticated");
            Require(response.Headers.TryGetValues("X-Correlation-ID", out var values)
                && values.Single() == "safe_Correlation-55",
                "HTTP pipeline preserves a validated correlation identifier");
        }
        using (var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me"))
        {
            request.Headers.Add("X-Correlation-ID", "unsafe value");
            using var response = await client.SendAsync(request);
            var generated = response.Headers.GetValues("X-Correlation-ID").Single();
            Require(generated.Length == 32 && generated.All(Uri.IsHexDigit),
                "HTTP pipeline replaces an invalid correlation identifier with a generated safe ID");
        }
        using (var response = await client.GetAsync("/_test/throw"))
        {
            Check(response, HttpStatusCode.InternalServerError, "HTTP exception handler converts unhandled errors to status 500");
            var body = await response.Content.ReadAsStringAsync();
            Require(body.Contains("internal_error", StringComparison.Ordinal)
                && !body.Contains("synthetic-secret-exception-detail", StringComparison.Ordinal),
                "HTTP exception responses redact internal exception details");
        }

        using (var response = await client.GetAsync("/api/v1/auth/me"))
            Check(response, HttpStatusCode.Unauthorized, "HTTP /me rejects missing credentials");
        using (var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me"))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", "synthetic");
            using var response = await client.SendAsync(request);
            Check(response, HttpStatusCode.Unauthorized, "HTTP /me rejects non-Bearer authorization");
        }
        using (var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me"))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", new string('z', 48));
            using var response = await client.SendAsync(request);
            Check(response, HttpStatusCode.Unauthorized, "HTTP /me rejects an unknown token");
        }
        using (var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me"))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await client.SendAsync(request);
            Check(response, HttpStatusCode.OK, "HTTP /me accepts a valid active session");
            var body = await response.Content.ReadAsStringAsync();
            Require(body.Contains(userId.ToString(), StringComparison.OrdinalIgnoreCase),
                "HTTP /me resolves the authenticated synthetic identity");
        }

        using var wrongPassword = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "active@example.invalid", password = "WrongSyntheticPass2026" });
        using var missingUser = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "missing@example.invalid", password });
        using var disabledLogin = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "disabled@example.invalid", password });
        using var lockedLogin = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "locked@example.invalid", password });
        Check(wrongPassword, HttpStatusCode.Unauthorized, "HTTP login rejects an incorrect password");
        Check(missingUser, HttpStatusCode.Unauthorized, "HTTP login rejects an unknown account");
        Check(disabledLogin, HttpStatusCode.Unauthorized, "HTTP login rejects a disabled account");
        Check(lockedLogin, HttpStatusCode.Unauthorized, "HTTP login rejects a locked account");
        Require(await wrongPassword.Content.ReadAsStringAsync() == await missingUser.Content.ReadAsStringAsync(),
            "HTTP login uses the same generic error body for wrong-password and unknown-user cases");

        using (var response = await client.GetAsync("/api/v1/hospitals/mine"))
            Check(response, HttpStatusCode.Unauthorized, "HTTP hospital list rejects anonymous callers");
        using (var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/hospitals/mine"))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await client.SendAsync(request);
            Check(response, HttpStatusCode.OK, "HTTP hospital list accepts an authenticated caller");
            var body = await response.Content.ReadAsStringAsync();
            Require(body.Contains(hospitalA.ToString(), StringComparison.OrdinalIgnoreCase)
                && !body.Contains(hospitalB.ToString(), StringComparison.OrdinalIgnoreCase),
                "HTTP hospital list contains only the caller's active membership");
        }
        using (var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/hospitals/context"))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await client.SendAsync(request);
            Check(response, HttpStatusCode.BadRequest, "HTTP hospital context requires X-Hospital-ID");
        }
        using (var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/hospitals/context"))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("X-Hospital-ID", hospitalA.ToString());
            using var response = await client.SendAsync(request);
            Check(response, HttpStatusCode.OK, "HTTP hospital context accepts an active membership");
        }
        using (var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/hospitals/context"))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("X-Hospital-ID", hospitalB.ToString());
            using var response = await client.SendAsync(request);
            Check(response, HttpStatusCode.Forbidden, "HTTP hospital context denies a tenant without membership");
        }

        const string expiredToken = "synthetic-expired-session-token-000000000000";
        sessions.Seed(SessionHash(expiredToken), new SessionRecord(Guid.NewGuid(), userId,
            DateTimeOffset.UtcNow.AddMinutes(-1), null));
        using (var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me"))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", expiredToken);
            using var response = await client.SendAsync(request);
            Check(response, HttpStatusCode.Unauthorized, "HTTP /me rejects an expired session");
        }

        using (var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout"))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await client.SendAsync(request);
            Check(response, HttpStatusCode.OK, "HTTP logout revokes the active session");
        }
        using (var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me"))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await client.SendAsync(request);
            Check(response, HttpStatusCode.Unauthorized, "HTTP /me rejects a session after logout");
        }
        using (var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout"))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await client.SendAsync(request);
            Check(response, HttpStatusCode.Unauthorized, "HTTP logout rejects an already revoked session");
        }

        const string disabledToken = "synthetic-disabled-account-session-000000000";
        sessions.Seed(SessionHash(disabledToken), new SessionRecord(Guid.NewGuid(), userId,
            DateTimeOffset.UtcNow.AddHours(1), null));
        users.SetStatus(userId, "disabled");
        using (var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me"))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", disabledToken);
            using var response = await client.SendAsync(request);
            Check(response, HttpStatusCode.Unauthorized, "HTTP /me rejects a user disabled after session creation");
        }

    }
    finally
    {
        await app.StopAsync();
    }
}

static string SessionHash(string token) =>
    Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();



static async Task RunLoginRateLimitHttpTestAsync()
{
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
    builder.Logging.ClearProviders();
    builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
    builder.Services.AddSingleton<IUserRepository>(new FakeUserRepository(Array.Empty<UserRecord>()));
    builder.Services.AddSingleton<ISessionRepository>(new FakeSessionRepository());
    builder.Services.AddScoped<Microsoft.AspNetCore.Identity.IPasswordHasher<AuthUser>, Microsoft.AspNetCore.Identity.PasswordHasher<AuthUser>>();
    builder.Services.AddScoped<PasswordHashService>();
    builder.Services.AddScopxRateLimiting();

    await using var app = builder.Build();
    app.UseScopxExceptionHandling();
    app.UseScopxSecurityHeaders();
    app.UseRateLimiter();
    app.MapAuthEndpoints();
    await app.StartAsync();
    try
    {
        var server = app.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>();
        using var client = new HttpClient { BaseAddress = new Uri(server.Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
        for (var attempt = 1; attempt <= 10; attempt++)
        {
            using var response = await client.PostAsJsonAsync("/api/v1/auth/login",
                new { email = "not-present@example.invalid", password = "NoSuchSyntheticPassword2026" });
            if (response.StatusCode != HttpStatusCode.Unauthorized)
                throw new InvalidOperationException($"FAIL: login request {attempt} should be permitted and rejected as invalid credentials; got {(int)response.StatusCode}.");
            Console.WriteLine($"PASS: login request {attempt} remains within the configured limit");
        }
        using (var overLimit = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "not-present@example.invalid", password = "NoSuchSyntheticPassword2026" }))
        {
            if (overLimit.StatusCode != HttpStatusCode.TooManyRequests)
                throw new InvalidOperationException($"FAIL: eleventh login request should be rate-limited; got {(int)overLimit.StatusCode}.");
            Console.WriteLine("PASS: login rate limiter rejects the eleventh request from one client");
            Require(overLimit.Headers.TryGetValues("X-Content-Type-Options", out var nosniff)
                && nosniff.Single() == "nosniff",
                "rate-limit rejection response retains the standard security headers");
        }
    }
    finally
    {
        await app.StopAsync();
    }
}


static async Task RunPostgresHttpIntegrationTestsAsync()
{
    var runtimeConnection = Environment.GetEnvironmentVariable("SCOPX_TEST_DB_RUNTIME_CONNECTION");
    var ownerConnection = Environment.GetEnvironmentVariable("SCOPX_TEST_DB_OWNER_CONNECTION");
    if (string.IsNullOrWhiteSpace(runtimeConnection) || string.IsNullOrWhiteSpace(ownerConnection))
    {
        Console.WriteLine("SKIP: PostgreSQL-backed HTTP integration tests (test connection variables are not configured).");
        return;
    }

    var userA = Guid.Parse("a1000000-0000-0000-0000-000000000001");
    var userB = Guid.Parse("a1000000-0000-0000-0000-000000000002");
    var disabledUser = Guid.Parse("a1000000-0000-0000-0000-000000000003");
    var lockedUser = Guid.Parse("a1000000-0000-0000-0000-000000000004");
    var hospitalA = Guid.Parse("b1000000-0000-0000-0000-000000000001");
    var hospitalB = Guid.Parse("b1000000-0000-0000-0000-000000000002");
    var membershipA = Guid.Parse("c1000000-0000-0000-0000-000000000001");
    var membershipB = Guid.Parse("c1000000-0000-0000-0000-000000000002");
    var expiredToken = "synthetic-postgres-expired-token-000000000000";
    const string password = "ScopxPostgresTest2026";
    var passwordHasher = new Microsoft.AspNetCore.Identity.PasswordHasher<AuthUser>();
    string HashFor(Guid id) => passwordHasher.HashPassword(new AuthUser { Id = id.ToString("N") }, password);

    await using (var owner = new NpgsqlConnection(ownerConnection))
    {
        await owner.OpenAsync();
        await using var tx = await owner.BeginTransactionAsync();
        async Task ExecuteSeed(string sql, params (string Name, object Value)[] parameters)
        {
            await using var command = new NpgsqlCommand(sql, owner, tx);
            foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Name, parameter.Value);
            await command.ExecuteNonQueryAsync();
        }

        await ExecuteSeed(
            "INSERT INTO public.users(id,email_normalized,display_name,status,password_hash) VALUES (@id,@email,@name,@status,@hash)",
            ("id", userA), ("email", "PG-AUTH-A@EXAMPLE.INVALID"), ("name", "Synthetic PostgreSQL User A"), ("status", "active"), ("hash", HashFor(userA)));
        await ExecuteSeed(
            "INSERT INTO public.users(id,email_normalized,display_name,status,password_hash) VALUES (@id,@email,@name,@status,@hash)",
            ("id", userB), ("email", "PG-AUTH-B@EXAMPLE.INVALID"), ("name", "Synthetic PostgreSQL User B"), ("status", "active"), ("hash", HashFor(userB)));
        await ExecuteSeed(
            "INSERT INTO public.users(id,email_normalized,display_name,status,password_hash) VALUES (@id,@email,@name,@status,@hash)",
            ("id", disabledUser), ("email", "PG-AUTH-DISABLED@EXAMPLE.INVALID"), ("name", "Synthetic Disabled User"), ("status", "disabled"), ("hash", HashFor(disabledUser)));
        await ExecuteSeed(
            "INSERT INTO public.users(id,email_normalized,display_name,status,password_hash,failed_login_count,lockout_until) VALUES (@id,@email,@name,'active',@hash,5,now()+interval '10 minutes')",
            ("id", lockedUser), ("email", "PG-AUTH-LOCKED@EXAMPLE.INVALID"), ("name", "Synthetic Locked User"), ("hash", HashFor(lockedUser)));

        await ExecuteSeed("INSERT INTO public.hospitals(id,name,status) VALUES (@id,@name,'active')",
            ("id", hospitalA), ("name", "Synthetic PostgreSQL Hospital A"));
        await ExecuteSeed("INSERT INTO public.hospitals(id,name,status) VALUES (@id,@name,'active')",
            ("id", hospitalB), ("name", "Synthetic PostgreSQL Hospital B"));
        await ExecuteSeed("INSERT INTO public.hospital_memberships(id,hospital_id,user_id,status) VALUES (@id,@hospital,@user,'active')",
            ("id", membershipA), ("hospital", hospitalA), ("user", userA));
        await ExecuteSeed("INSERT INTO public.hospital_memberships(id,hospital_id,user_id,status) VALUES (@id,@hospital,@user,'active')",
            ("id", membershipB), ("hospital", hospitalB), ("user", userB));
        await ExecuteSeed(
            "INSERT INTO public.membership_roles(hospital_id,membership_id,role_id) SELECT @hospital,@membership,id FROM public.roles WHERE code='doctor'",
            ("hospital", hospitalA), ("membership", membershipA));
        await ExecuteSeed(
            "INSERT INTO public.membership_roles(hospital_id,membership_id,role_id) SELECT @hospital,@membership,id FROM public.roles WHERE code='nurse'",
            ("hospital", hospitalB), ("membership", membershipB));
        await ExecuteSeed(
            "INSERT INTO public.user_sessions(id,user_id,token_hash,created_at,expires_at) VALUES (@id,@user,@hash,now()-interval '2 hours',now()-interval '1 hour')",
            ("id", Guid.Parse("d1000000-0000-0000-0000-000000000001")), ("user", userA), ("hash", SessionHash(expiredToken)));
        await tx.CommitAsync();
    }

    try
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Services.AddSingleton<IUserRepository>(_ => new NpgsqlUserRepository(runtimeConnection));
        builder.Services.AddSingleton<ISessionRepository>(_ => new NpgsqlSessionRepository(runtimeConnection));
        builder.Services.AddSingleton<IHospitalMembershipRepository>(_ => new NpgsqlHospitalMembershipRepository(runtimeConnection));
        builder.Services.AddScoped<Microsoft.AspNetCore.Identity.IPasswordHasher<AuthUser>, Microsoft.AspNetCore.Identity.PasswordHasher<AuthUser>>();
        builder.Services.AddScoped<PasswordHashService>();
        builder.Services.AddScopxRateLimiting();
        builder.Services.AddScopxCors(builder.Configuration);

        await using var app = builder.Build();
        app.UseScopxExceptionHandling();
        app.UseScopxSecurityHeaders();
        app.UseCors("scopx");
        app.UseRateLimiter();
        app.MapAuthEndpoints();
        app.MapHospitalContextEndpoints();
        await app.StartAsync();
        try
        {
            var server = app.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>();
            using var client = new HttpClient { BaseAddress = new Uri(server.Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
            static void Check(HttpResponseMessage response, HttpStatusCode expected, string label)
            {
                if (response.StatusCode != expected)
                    throw new InvalidOperationException($"FAIL: {label}; expected {(int)expected}, got {(int)response.StatusCode}.");
                Console.WriteLine("PASS: " + label);
            }

            using (var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me"))
            {
                request.Headers.Add("Origin", "https://app.scopx.test");
                using var response = await client.SendAsync(request);
                Check(response, HttpStatusCode.Unauthorized, "PostgreSQL test host still requires authentication");
                Require(!response.Headers.Contains("Access-Control-Allow-Origin"),
                    "CORS denies every browser origin when no allowlist is configured");
            }

            using var loginA = await client.PostAsJsonAsync("/api/v1/auth/login",
                new { email = "pg-auth-a@example.invalid", password });
            Check(loginA, HttpStatusCode.OK, "PostgreSQL runtime role can authenticate a synthetic active user");
            using var jsonA = JsonDocument.Parse(await loginA.Content.ReadAsStringAsync());
            var tokenA = jsonA.RootElement.GetProperty("access_token").GetString()
                ?? throw new InvalidOperationException("PostgreSQL-backed login did not return a token.");

            using (var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me"))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
                using var response = await client.SendAsync(request);
                Check(response, HttpStatusCode.OK, "PostgreSQL-backed /me resolves a persisted session");
            }
            using (var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/hospitals/mine"))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
                using var response = await client.SendAsync(request);
                Check(response, HttpStatusCode.OK, "Npgsql membership repository lists active memberships under runtime grants");
                var body = await response.Content.ReadAsStringAsync();
                Require(body.Contains(hospitalA.ToString(), StringComparison.OrdinalIgnoreCase)
                    && !body.Contains(hospitalB.ToString(), StringComparison.OrdinalIgnoreCase),
                    "PostgreSQL RLS and membership query isolate hospital A from hospital B");
            }
            using (var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/hospitals/context"))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
                request.Headers.Add("X-Hospital-ID", hospitalA.ToString());
                using var response = await client.SendAsync(request);
                Check(response, HttpStatusCode.OK, "Npgsql hospital-context repository accepts the user's own tenant");
            }
            using (var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/hospitals/context"))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
                request.Headers.Add("X-Hospital-ID", hospitalB.ToString());
                using var response = await client.SendAsync(request);
                Check(response, HttpStatusCode.Forbidden, "Npgsql hospital-context repository denies another user's tenant");
            }
            using var loginB = await client.PostAsJsonAsync("/api/v1/auth/login",
                new { email = "pg-auth-b@example.invalid", password });
            Check(loginB, HttpStatusCode.OK, "PostgreSQL runtime role authenticates a second synthetic tenant user");
            using var jsonB = JsonDocument.Parse(await loginB.Content.ReadAsStringAsync());
            var tokenB = jsonB.RootElement.GetProperty("access_token").GetString()
                ?? throw new InvalidOperationException("Second PostgreSQL-backed login did not return a token.");
            using (var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/hospitals/mine"))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
                using var response = await client.SendAsync(request);
                Check(response, HttpStatusCode.OK, "Second user's membership list is available to that user");
                var body = await response.Content.ReadAsStringAsync();
                Require(body.Contains(hospitalB.ToString(), StringComparison.OrdinalIgnoreCase)
                    && !body.Contains(hospitalA.ToString(), StringComparison.OrdinalIgnoreCase),
                    "PostgreSQL RLS isolates hospital B from hospital A for the second user");
            }
            using (var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/hospitals/context"))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
                request.Headers.Add("X-Hospital-ID", hospitalA.ToString());
                using var response = await client.SendAsync(request);
                Check(response, HttpStatusCode.Forbidden, "Second user cannot select the first user's hospital");
            }
            using (var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout"))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
                using var response = await client.SendAsync(request);
                Check(response, HttpStatusCode.OK, "Second user's PostgreSQL session can be revoked");
            }

            await using (var ownerUpdate = new NpgsqlConnection(ownerConnection))
            {
                await ownerUpdate.OpenAsync();
                await using var suspend = new NpgsqlCommand(
                    "UPDATE public.hospital_memberships SET status='suspended' WHERE id=@membership", ownerUpdate);
                suspend.Parameters.AddWithValue("membership", membershipA);
                Require(await suspend.ExecuteNonQueryAsync() == 1,
                    "Synthetic hospital membership can be suspended by the test fixture owner");
            }
            using (var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/hospitals/context"))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
                request.Headers.Add("X-Hospital-ID", hospitalA.ToString());
                using var response = await client.SendAsync(request);
                Check(response, HttpStatusCode.Forbidden, "PostgreSQL-backed API immediately denies a suspended membership");
            }
            using (var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me"))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
                using var response = await client.SendAsync(request);
                Check(response, HttpStatusCode.OK, "Suspending a hospital membership does not revoke the user's independent login session");
            }

            for (var attempt = 1; attempt <= LoginLockoutPolicy.MaxFailedAttemptsBeforeLockout; attempt++)
            {
                using var badLogin = await client.PostAsJsonAsync("/api/v1/auth/login",
                    new { email = "pg-auth-a@example.invalid", password = "WrongPostgresPassword2026" });
                Check(badLogin, HttpStatusCode.Unauthorized, $"PostgreSQL-backed failed password attempt {attempt} is rejected");
            }
            var persistedUser = await new NpgsqlUserRepository(runtimeConnection)
                .FindByNormalizedEmailAsync("PG-AUTH-A@EXAMPLE.INVALID");
            Require(persistedUser is not null
                && persistedUser.FailedLoginCount == LoginLockoutPolicy.MaxFailedAttemptsBeforeLockout
                && persistedUser.LockoutUntil is DateTimeOffset lockoutUntil
                && lockoutUntil > DateTimeOffset.UtcNow,
                "PostgreSQL repository persists the failed-login counter and active lockout deadline");
            using (var lockedAfterFailures = await client.PostAsJsonAsync("/api/v1/auth/login",
                new { email = "pg-auth-a@example.invalid", password }))
                Check(lockedAfterFailures, HttpStatusCode.Unauthorized, "PostgreSQL-backed login enforces the persisted lockout after repeated failures");
            using (var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me"))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", expiredToken);
                using var response = await client.SendAsync(request);
                Check(response, HttpStatusCode.Unauthorized, "PostgreSQL session repository rejects an expired persisted session");
            }
            using var disabled = await client.PostAsJsonAsync("/api/v1/auth/login",
                new { email = "pg-auth-disabled@example.invalid", password });
            Check(disabled, HttpStatusCode.Unauthorized, "PostgreSQL-backed login rejects a disabled user");
            using var locked = await client.PostAsJsonAsync("/api/v1/auth/login",
                new { email = "pg-auth-locked@example.invalid", password });
            Check(locked, HttpStatusCode.Unauthorized, "PostgreSQL-backed login rejects a locked user");
            using (var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout"))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
                using var response = await client.SendAsync(request);
                Check(response, HttpStatusCode.OK, "PostgreSQL-backed logout persists session revocation");
            }
            using (var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me"))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
                using var response = await client.SendAsync(request);
                Check(response, HttpStatusCode.Unauthorized, "PostgreSQL-backed /me rejects a revoked persisted session");
            }
        }
        finally
        {
            await app.StopAsync();
        }
    }
    finally
    {
        await using var cleanup = new NpgsqlConnection(ownerConnection);
        await cleanup.OpenAsync();
        await using (var deleteRoles = new NpgsqlCommand(
            "DELETE FROM public.membership_roles WHERE membership_id IN (@a,@b)", cleanup))
        {
            deleteRoles.Parameters.AddWithValue("a", membershipA);
            deleteRoles.Parameters.AddWithValue("b", membershipB);
            await deleteRoles.ExecuteNonQueryAsync();
        }
        await using (var deleteMemberships = new NpgsqlCommand(
            "DELETE FROM public.hospital_memberships WHERE id IN (@a,@b)", cleanup))
        {
            deleteMemberships.Parameters.AddWithValue("a", membershipA);
            deleteMemberships.Parameters.AddWithValue("b", membershipB);
            await deleteMemberships.ExecuteNonQueryAsync();
        }
        await using (var deleteSessions = new NpgsqlCommand(
            "DELETE FROM public.user_sessions WHERE user_id IN (@a,@b,@c,@d)", cleanup))
        {
            deleteSessions.Parameters.AddWithValue("a", userA);
            deleteSessions.Parameters.AddWithValue("b", userB);
            deleteSessions.Parameters.AddWithValue("c", disabledUser);
            deleteSessions.Parameters.AddWithValue("d", lockedUser);
            await deleteSessions.ExecuteNonQueryAsync();
        }
        await using (var deleteHospitals = new NpgsqlCommand(
            "DELETE FROM public.hospitals WHERE id IN (@a,@b)", cleanup))
        {
            deleteHospitals.Parameters.AddWithValue("a", hospitalA);
            deleteHospitals.Parameters.AddWithValue("b", hospitalB);
            await deleteHospitals.ExecuteNonQueryAsync();
        }
        await using (var deleteUsers = new NpgsqlCommand(
            "DELETE FROM public.users WHERE id IN (@a,@b,@c,@d)", cleanup))
        {
            deleteUsers.Parameters.AddWithValue("a", userA);
            deleteUsers.Parameters.AddWithValue("b", userB);
            deleteUsers.Parameters.AddWithValue("c", disabledUser);
            deleteUsers.Parameters.AddWithValue("d", lockedUser);
            await deleteUsers.ExecuteNonQueryAsync();
        }
    }
}

sealed class FakeUserRepository(IEnumerable<UserRecord> seed) : IUserRepository
{
    private readonly Dictionary<Guid, UserRecord> _byId = seed.ToDictionary(x => x.Id);
    public Task<UserRecord?> FindByNormalizedEmailAsync(string email, CancellationToken ct = default) =>
        Task.FromResult(_byId.Values.SingleOrDefault(x => x.NormalizedEmail == email));
    public Task<UserRecord?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_byId.GetValueOrDefault(id));
    public Task<UserRecord?> IncrementFailedLoginAsync(Guid id, CancellationToken ct = default)
    {
        if (!_byId.TryGetValue(id, out var user)) return Task.FromResult<UserRecord?>(null);
        var count = user.FailedLoginCount + 1;
        var until = count >= LoginLockoutPolicy.MaxFailedAttemptsBeforeLockout
            ? DateTimeOffset.UtcNow.Add(LoginLockoutPolicy.LockoutFor(count)) : user.LockoutUntil;
        var updated = user with { FailedLoginCount = count, LockoutUntil = until };
        _byId[id] = updated;
        return Task.FromResult<UserRecord?>(updated);
    }
    public Task<bool> RecordFailedLoginAsync(Guid id, int count, DateTimeOffset? until, CancellationToken ct = default)
    {
        if (!_byId.TryGetValue(id, out var user)) return Task.FromResult(false);
        _byId[id] = user with { FailedLoginCount = count, LockoutUntil = until };
        return Task.FromResult(true);
    }
    public Task<bool> RecordSuccessfulLoginAsync(Guid id, CancellationToken ct = default)
    {
        if (!_byId.TryGetValue(id, out var user) || user.Status != "active" || LoginLockoutPolicy.IsLocked(user.LockoutUntil, DateTimeOffset.UtcNow))
            return Task.FromResult(false);
        _byId[id] = user with { FailedLoginCount = 0, LockoutUntil = null, LastLoginAt = DateTimeOffset.UtcNow };
        return Task.FromResult(true);
    }
    public void SetStatus(Guid id, string status)
    {
        if (_byId.TryGetValue(id, out var user)) _byId[id] = user with { Status = status };
    }
}

sealed class FakeSessionRepository : ISessionRepository
{
    private readonly Dictionary<string, SessionRecord> _sessions = new(StringComparer.Ordinal);
    public Task CreateAsync(Guid userId, string hash, DateTimeOffset expiry, CancellationToken ct = default)
    {
        _sessions[hash] = new SessionRecord(Guid.NewGuid(), userId, expiry, null);
        return Task.CompletedTask;
    }
    public Task<SessionRecord?> FindActiveByTokenHashAsync(string hash, DateTimeOffset now, CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(hash, out var session) || session.RevokedAt is not null || session.ExpiresAt <= now)
            return Task.FromResult<SessionRecord?>(null);
        return Task.FromResult<SessionRecord?>(session);
    }
    public Task<bool> RevokeByTokenHashAsync(string hash, DateTimeOffset now, CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(hash, out var session) || session.RevokedAt is not null) return Task.FromResult(false);
        _sessions[hash] = session with { RevokedAt = now };
        return Task.FromResult(true);
    }
    public void Seed(string hash, SessionRecord session) => _sessions[hash] = session;
}

sealed class FakeHospitalMembershipRepository(Guid ownerId, IEnumerable<HospitalMembershipRecord> seed) : IHospitalMembershipRepository
{
    private readonly List<HospitalMembershipRecord> _memberships = [.. seed];
    public Task<IReadOnlyList<HospitalMembershipRecord>> ListActiveForUserAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<HospitalMembershipRecord>>(userId == ownerId
            ? _memberships.Where(x => x.MembershipStatus == "active").ToArray()
            : []);
    public Task<HospitalMembershipRecord?> FindActiveForUserAndHospitalAsync(Guid userId, Guid hospitalId, CancellationToken ct = default) =>
        Task.FromResult(userId == ownerId
            ? _memberships.SingleOrDefault(x => x.HospitalId == hospitalId && x.MembershipStatus == "active")
            : null);
}



sealed class FaultedCaptureAdapter : IDeviceCaptureAdapter
{
    public int StartCalls { get; private set; }
    public int StopCalls { get; private set; }
    public CaptureState ReportedState { get; set; } = CaptureState.Faulted;
    public CaptureDeviceDescriptor Descriptor => new(
        "usb:synthetic-faulted-device", "Synthetic Faulted Capture Adapter",
        CaptureTransport.Usb, "Synthetic Vendor", "Fault Test", CaptureState.Faulted);

    public Task<CaptureStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new CaptureStatus(ReportedState, "Synthetic adapter state", DateTimeOffset.UtcNow));
    }

    public Task StartAsync(CaptureStartOptions options, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StartCalls++;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StopCalls++;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
