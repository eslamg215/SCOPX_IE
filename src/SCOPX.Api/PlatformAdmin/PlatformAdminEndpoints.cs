using Npgsql;
using SCOPX.Api.Authentication;
using SCOPX.Infrastructure.Sessions;

namespace SCOPX.Api.PlatformAdmin;

/// <summary>Read-only platform administration endpoints. Draft only; writes are deferred until audit/approval workflow exists.</summary>
public static class PlatformAdminEndpoints
{
    public static IEndpointRouteBuilder MapPlatformAdminEndpoints(this IEndpointRouteBuilder endpoints, string connectionString)
    {
        var group = endpoints.MapGroup("/api/v1/platform-admin").RequireRateLimiting("api");
        group.MapGet("/overview", async (HttpContext context, CancellationToken ct) =>
        {
            if (!await IsPlatformAdminAsync(context, connectionString, ct)) return (IResult)Results.Forbid();
            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(@"SELECT
                (SELECT count(*) FROM hospitals) AS hospitals_total,
                (SELECT count(*) FROM hospitals WHERE status='active') AS hospitals_active,
                (SELECT count(*) FROM users) AS users_total,
                (SELECT count(*) FROM users WHERE status='active') AS users_active,
                (SELECT count(*) FROM hospital_memberships WHERE status='active') AS memberships_active,
                (SELECT count(*) FROM hospital_licenses WHERE status='active') AS licenses_active", conn);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return (IResult)Results.Problem("Unable to read platform overview.");
            return Results.Ok(new { hospitals_total = reader.GetInt64(0), hospitals_active = reader.GetInt64(1), users_total = reader.GetInt64(2), users_active = reader.GetInt64(3), memberships_active = reader.GetInt64(4), licenses_active = reader.GetInt64(5) });
        });

        group.MapGet("/hospitals", async (HttpContext context, int? limit, int? offset, CancellationToken ct) =>
        {
            if (!await IsPlatformAdminAsync(context, connectionString, ct)) return (IResult)Results.Forbid();
            var take = Math.Clamp(limit ?? 25, 1, 100); var skip = Math.Max(offset ?? 0, 0);
            await using var conn = new NpgsqlConnection(connectionString); await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(@"SELECT h.id, h.name, h.status, h.created_at,
                count(DISTINCT hm.id) FILTER (WHERE hm.status='active') AS active_members,
                count(DISTINCT l.id) FILTER (WHERE l.status='active') AS active_licenses
                FROM hospitals h LEFT JOIN hospital_memberships hm ON hm.hospital_id=h.id
                LEFT JOIN hospital_licenses l ON l.hospital_id=h.id
                GROUP BY h.id ORDER BY h.created_at DESC LIMIT @take OFFSET @skip", conn);
            cmd.Parameters.AddWithValue("take", take); cmd.Parameters.AddWithValue("skip", skip);
            await using var reader = await cmd.ExecuteReaderAsync(ct); var rows = new List<object>();
            while (await reader.ReadAsync(ct)) rows.Add(new { id = reader.GetGuid(0), name = reader.GetString(1), status = reader.GetString(2), created_at = reader.GetDateTime(3), active_members = reader.GetInt64(4), active_licenses = reader.GetInt64(5) });
            return Results.Ok(rows);
        });

        group.MapGet("/users", async (HttpContext context, string? q, int? limit, int? offset, CancellationToken ct) =>
        {
            if (!await IsPlatformAdminAsync(context, connectionString, ct)) return (IResult)Results.Forbid();
            var take = Math.Clamp(limit ?? 25, 1, 100); var skip = Math.Max(offset ?? 0, 0); var search = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
            await using var conn = new NpgsqlConnection(connectionString); await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(@"SELECT id, email_normalized, display_name, status, created_at FROM users
                WHERE @q IS NULL OR email_normalized ILIKE '%' || @q || '%' OR display_name ILIKE '%' || @q || '%'
                ORDER BY created_at DESC LIMIT @take OFFSET @skip", conn);
            cmd.Parameters.AddWithValue("q", (object?)search ?? DBNull.Value); cmd.Parameters.AddWithValue("take", take); cmd.Parameters.AddWithValue("skip", skip);
            await using var reader = await cmd.ExecuteReaderAsync(ct); var rows = new List<object>();
            while (await reader.ReadAsync(ct)) rows.Add(new { id = reader.GetGuid(0), email = reader.GetString(1), display_name = reader.GetString(2), status = reader.GetString(3), created_at = reader.GetDateTime(4) });
            return Results.Ok(rows);
        });
        return endpoints;
    }

    private static async Task<bool> IsPlatformAdminAsync(HttpContext context, string connectionString, CancellationToken ct)
    {
        var token = AuthHeaderReader.ReadBearer(context.Request.Headers.Authorization.ToString());
        if (token is null) return false;
        await using var conn = new NpgsqlConnection(connectionString); await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(@"SELECT EXISTS (
            SELECT 1 FROM user_sessions s JOIN users u ON u.id=s.user_id
            JOIN platform_user_roles pur ON pur.user_id=u.id JOIN roles r ON r.id=pur.role_id
            WHERE s.token_hash=@hash AND s.revoked_at IS NULL AND s.expires_at>@now AND u.status='active'
            AND r.code='platform_super_admin')", conn);
        cmd.Parameters.AddWithValue("hash", SessionToken.Hash(token)); cmd.Parameters.AddWithValue("now", DateTimeOffset.UtcNow);
        return (bool)(await cmd.ExecuteScalarAsync(ct) ?? false);
    }
}
