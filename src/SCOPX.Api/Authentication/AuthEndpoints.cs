using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using SCOPX.Api.Security;
using SCOPX.Infrastructure.Users;
using SCOPX.Infrastructure.Sessions;
using System.Security.Claims;
namespace SCOPX.Api.Authentication;

public static class AuthEndpoints
{
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(8);
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/auth").RequireRateLimiting("api");
        group.MapPost("/login", async (LoginRequest input, IUserRepository users, ISessionRepository sessions, PasswordHashService hashes, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(input.Email) || string.IsNullOrWhiteSpace(input.Password) || input.Email.Length > 320 || input.Password.Length > PasswordPolicy.MaximumLength)
                return Results.BadRequest(new { error = "invalid_credentials" });
            var email = input.Email.Trim().ToUpperInvariant();
            var user = await users.FindByNormalizedEmailAsync(email, ct);
            // Deliberately generic response for missing, disabled, locked, or invalid-password accounts.
            if (user is null || user.Status != "active" || string.IsNullOrEmpty(user.PasswordHash) || LoginLockoutPolicy.IsLocked(user.LockoutUntil, DateTimeOffset.UtcNow))
                return Results.Json(new { error = "invalid_credentials" }, statusCode: StatusCodes.Status401Unauthorized);
            var subject = new AuthUser { Id = user.Id.ToString("N") };
            var verification = hashes.Verify(subject, user.PasswordHash, input.Password);
            if (verification == PasswordVerificationResult.Failed)
            {
                await users.IncrementFailedLoginAsync(user.Id, ct); // Atomic database-side counter and lockout calculation.
                return Results.Json(new { error = "invalid_credentials" }, statusCode: StatusCodes.Status401Unauthorized);
            }
            if (!await users.RecordSuccessfulLoginAsync(user.Id, ct))
                return Results.Json(new { error = "invalid_credentials" }, statusCode: StatusCodes.Status401Unauthorized);
            var token = SessionToken.Create();
            var expiry = DateTimeOffset.UtcNow.Add(SessionLifetime);
            await sessions.CreateAsync(user.Id, SessionToken.Hash(token), expiry, ct);
            return Results.Ok(new { access_token = token, token_type = "Bearer", expires_at = expiry });
        });
        group.MapGet("/me", async (HttpContext context, IUserRepository users, ISessionRepository sessions, CancellationToken ct) =>
        {
            var token = AuthHeaderReader.ReadBearer(context.Request.Headers.Authorization.ToString());
            if (token is null) return Results.Unauthorized();
            var session = await sessions.FindActiveByTokenHashAsync(SessionToken.Hash(token), DateTimeOffset.UtcNow, ct);
            if (session is null) return Results.Unauthorized();
            var user = await users.FindByIdAsync(session.UserId, ct);
            if (user is null || user.Status != "active") return Results.Unauthorized();
            return Results.Ok(new { user_id = user.Id, email = user.NormalizedEmail, display_name = user.DisplayName, session_expires_at = session.ExpiresAt });
        });
        group.MapPost("/logout", async (HttpContext context, ISessionRepository sessions, CancellationToken ct) =>
        {
            var token = AuthHeaderReader.ReadBearer(context.Request.Headers.Authorization.ToString());
            if (token is null) return Results.Unauthorized();
            var revoked = await sessions.RevokeByTokenHashAsync(SessionToken.Hash(token), DateTimeOffset.UtcNow, ct);
            return revoked ? Results.Ok(new { status = "logged_out" }) : Results.Unauthorized();
        });
        return endpoints;
    }
    public sealed record LoginRequest(string Email, string Password);
}

public static class AuthHeaderReader
{
    public static string? ReadBearer(string header)
    {
        const string prefix = "Bearer ";
        if (!header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
        var token = header[prefix.Length..].Trim();
        return token.Length is >= 30 and <= 128 ? token : null;
    }
}
