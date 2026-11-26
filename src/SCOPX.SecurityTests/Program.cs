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

await RunAuthHttpIntegrationTestsAsync();
await RunLoginRateLimitHttpTestAsync();
await RunPostgresHttpIntegrationTestsAsync();


static async Task RunAuthHttpIntegrationTestsAsync()
{
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
    builder.Logging.ClearProviders();
    builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
    builder.Services.AddSingleton<IUserRepository>(users);
    builder.Services.AddSingleton<ISessionRepository>(sessions);
    builder.Services.AddSingleton<IHospitalMembershipRepository>(memberships);
    builder.Services.AddScoped<Microsoft.AspNetCore.Identity.IPasswordHasher<AuthUser>, Microsoft.AspNetCore.Identity.PasswordHasher<AuthUser>>();
    builder.Services.AddScoped<PasswordHashService>();
    builder.Services.AddScopxRateLimiting();

    await using var app = builder.Build();
    app.UseScopxExceptionHandling();
    app.UseScopxSecurityHeaders();
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

        await using var app = builder.Build();
        app.UseScopxExceptionHandling();
        app.UseScopxSecurityHeaders();
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

