using System.Security.Cryptography;
using SCOPX.Infrastructure.Media;
using SCOPX.Infrastructure.Hospitals;
using SCOPX.Infrastructure.Sessions;
using SCOPX.Infrastructure.Users;
using SCOPX.Api.Authentication;
using SCOPX.Api.Security;

namespace SCOPX.Api.Media;

public static class MediaEndpoints
{
    private const long MaxBytes = 50 * 1024 * 1024;

    public static IEndpointRouteBuilder MapMediaEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/media").RequireRateLimiting("api");

        group.MapGet("/procedure/{procedureId:guid}",
            async (Guid procedureId, HttpContext context, IUserRepository users, ISessionRepository sessions,
                IHospitalMembershipRepository memberships, IMediaRepository media, CancellationToken ct) =>
            {
                var actor = await Resolve(context, users, sessions, memberships, ct);
                if (actor is null) return Results.Unauthorized();
                if (!ReadAllowed(actor.Roles)) return Results.Forbid();
                if (!await media.ProcedureExistsAsync(actor.HospitalId, procedureId, ct)) return Results.NotFound();
                return Results.Ok(await media.ListForProcedureAsync(actor.HospitalId, actor.UserId, procedureId, ct));
            });

        group.MapPost("/upload/{procedureId:guid}",
            async (Guid procedureId, HttpContext context, IUserRepository users, ISessionRepository sessions,
                IHospitalMembershipRepository memberships, IMediaRepository media, MediaContentProtector protector,
                IMediaMalwareScanner scanner, ILoggerFactory loggerFactory, CancellationToken ct) =>
            {
                var logger = loggerFactory.CreateLogger("SCOPX.Api.Media.Upload");
                var actor = await Resolve(context, users, sessions, memberships, ct);
                if (actor is null) return Results.Unauthorized();
                if (!WriteAllowed(actor.Roles)) return Results.Forbid();
                if (!await media.ProcedureExistsAsync(actor.HospitalId, procedureId, ct))
                    return Results.NotFound(new { error = "procedure_not_found" });

                var file = context.Request.Form.Files.FirstOrDefault();
                if (file is null || file.Length <= 0 || file.Length > MaxBytes)
                    return Results.BadRequest(new { error = "invalid_file_size", maxBytes = MaxBytes });

                var normalizedName = file.FileName.Replace('\\', '/');
                var safeName = Path.GetFileName(normalizedName);
                var extension = Path.GetExtension(safeName);
                if (string.IsNullOrWhiteSpace(safeName) || safeName.Length > 255)
                    return Results.BadRequest(new { error = "invalid_file_name" });

                var root = context.RequestServices.GetRequiredService<IConfiguration>()["MediaStorage:Root"];
                if (string.IsNullOrWhiteSpace(root))
                    return Results.Json(new { error = "media_storage_not_configured" }, statusCode: StatusCodes.Status503ServiceUnavailable);

                var mediaId = Guid.NewGuid();
                var storageKey = $"{actor.HospitalId:N}/{mediaId:N}.scx";
                if (!TryResolvePath(root, storageKey, out var fullPath))
                    return Results.Problem(statusCode: StatusCodes.Status500InternalServerError);

                var plaintext = new byte[checked((int)file.Length)];
                byte[] encrypted;
                string sha256;
                string mediaType;
                string contentType;
                try
                {
                    await using (var input = file.OpenReadStream())
                        await input.ReadExactlyAsync(plaintext, ct);

                    if (!MediaFileSignatureValidator.Matches(extension, plaintext, out mediaType, out contentType))
                        return Results.BadRequest(new { error = "file_signature_mismatch" });

                    try
                    {
                        if (!await scanner.IsCleanAsync(plaintext, ct))
                            return Results.UnprocessableEntity(new { error = "malware_detected" });
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.LogWarning("Media malware scanning is unavailable ({ExceptionType}); upload rejected.", ex.GetType().Name);
                        return Results.Json(new { error = "media_scanning_unavailable" }, statusCode: StatusCodes.Status503ServiceUnavailable);
                    }

                    try
                    {
                        encrypted = protector.Encrypt(actor.HospitalId, mediaId, plaintext);
                        sha256 = Convert.ToHexString(SHA256.HashData(plaintext)).ToLowerInvariant();
                    }
                    catch (Exception ex) when (ex is InvalidOperationException or CryptographicException)
                    {
                        logger.LogError("Media encryption is not configured or failed ({ExceptionType}).", ex.GetType().Name);
                        return Results.Json(new { error = "media_encryption_unavailable" }, statusCode: StatusCodes.Status503ServiceUnavailable);
                    }
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(plaintext);
                }

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                    await using (var output = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                        81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
                        await output.WriteAsync(encrypted, ct);

                    var record = await media.CreateAsync(actor.UserId,
                        new MediaUploadMetadata(actor.HospitalId, procedureId, mediaId, storageKey, mediaType,
                            safeName, contentType, file.Length, sha256), ct);

                    if (record is null)
                    {
                        File.Delete(fullPath);
                        return Results.Problem(statusCode: StatusCodes.Status500InternalServerError);
                    }

                    return Results.Created($"/api/v1/media/{record.Id}", record);
                }
                catch
                {
                    if (File.Exists(fullPath)) File.Delete(fullPath);
                    throw;
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(encrypted);
                }
            }).DisableAntiforgery();

        group.MapGet("/{id:guid}/download",
            async (Guid id, HttpContext context, IUserRepository users, ISessionRepository sessions,
                IHospitalMembershipRepository memberships, IMediaRepository media, MediaContentProtector protector,
                ILoggerFactory loggerFactory, CancellationToken ct) =>
            {
                var logger = loggerFactory.CreateLogger("SCOPX.Api.Media.Download");
                var actor = await Resolve(context, users, sessions, memberships, ct);
                if (actor is null) return Results.Unauthorized();
                if (!ReadAllowed(actor.Roles)) return Results.Forbid();

                var found = await media.FindAsync(actor.HospitalId, actor.UserId, id, ct);
                if (found.Asset is null || found.StorageKey is null) return Results.NotFound();

                var root = context.RequestServices.GetRequiredService<IConfiguration>()["MediaStorage:Root"];
                if (string.IsNullOrWhiteSpace(root) || !TryResolvePath(root, found.StorageKey, out var fullPath) || !File.Exists(fullPath))
                    return Results.NotFound();

                try
                {
                    var encrypted = await File.ReadAllBytesAsync(fullPath, ct);
                    var plaintext = protector.Decrypt(actor.HospitalId, id, encrypted);
                    CryptographicOperations.ZeroMemory(encrypted);

                    var actualHash = SHA256.HashData(plaintext);
                    byte[] expectedHash;
                    try { expectedHash = Convert.FromHexString(found.Asset.Sha256Hex); }
                    catch (FormatException)
                    {
                        CryptographicOperations.ZeroMemory(plaintext);
                        return Results.Problem(statusCode: StatusCodes.Status500InternalServerError);
                    }

                    if (found.Asset.FileSizeBytes != plaintext.LongLength ||
                        !CryptographicOperations.FixedTimeEquals(actualHash, expectedHash))
                    {
                        CryptographicOperations.ZeroMemory(plaintext);
                        return Results.Problem(statusCode: StatusCodes.Status500InternalServerError);
                    }

                    return Results.File(plaintext, found.Asset.ContentType, found.Asset.FileName,
                        enableRangeProcessing: false);
                }
                catch (Exception ex) when (ex is CryptographicException or IOException or InvalidOperationException)
                {
                    logger.LogError("Protected media could not be decrypted or verified ({ExceptionType}).", ex.GetType().Name);
                    return Results.Problem(statusCode: StatusCodes.Status500InternalServerError);
                }
            });

        return endpoints;
    }

    private static bool TryResolvePath(string root, string key, out string fullPath)
    {
        var rootFull = Path.GetFullPath(root);
        fullPath = Path.GetFullPath(Path.Combine(rootFull, key.Replace('/', Path.DirectorySeparatorChar)));
        var relative = Path.GetRelativePath(rootFull, fullPath);
        return !Path.IsPathRooted(relative) && relative != ".." &&
            !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    private static async Task<AuthContext?> Resolve(HttpContext context, IUserRepository users,
        ISessionRepository sessions, IHospitalMembershipRepository memberships, CancellationToken ct)
    {
        var token = AuthHeaderReader.ReadBearer(context.Request.Headers.Authorization.ToString());
        if (token is null) return null;

        var session = await sessions.FindActiveByTokenHashAsync(SessionToken.Hash(token), DateTimeOffset.UtcNow, ct);
        if (session is null) return null;

        var user = await users.FindByIdAsync(session.UserId, ct);
        if (user is not { Status: "active" }) return null;

        if (!Guid.TryParse(context.Request.Headers["X-Hospital-ID"].ToString(), out var hospitalId))
            return null;

        var membership = await memberships.FindActiveForUserAndHospitalAsync(user.Id, hospitalId, ct);
        return membership is null ? null : new AuthContext(user.Id, hospitalId, membership.RoleCodes.ToArray());
    }

    private static bool ReadAllowed(string[] roles) =>
        roles.Any(role => new[] { "hospital_admin", "doctor", "nurse", "auditor", "endoscopy_technician" }
            .Contains(role, StringComparer.OrdinalIgnoreCase));

    private static bool WriteAllowed(string[] roles) =>
        roles.Any(role => new[] { "hospital_admin", "doctor", "endoscopy_technician" }
            .Contains(role, StringComparer.OrdinalIgnoreCase));

    private sealed record AuthContext(Guid UserId, Guid HospitalId, string[] Roles);
}
