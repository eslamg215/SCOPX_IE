using Npgsql;
using NpgsqlTypes;
namespace SCOPX.Infrastructure.Sessions;


/// <summary>Persistence for opaque sessions. Raw bearer tokens are never stored in the database.</summary>
public sealed class NpgsqlSessionRepository(string connectionString) : ISessionRepository
{
    public async Task CreateAsync(Guid userId, string tokenHash, DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("INSERT INTO public.user_sessions(user_id, token_hash, expires_at) VALUES (@user, @hash, @expires)", connection);
        command.Parameters.AddWithValue("user", NpgsqlDbType.Uuid, userId);
        command.Parameters.AddWithValue("hash", NpgsqlDbType.Char, tokenHash);
        command.Parameters.AddWithValue("expires", NpgsqlDbType.TimestampTz, expiresAt.UtcDateTime);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
    public async Task<SessionRecord?> FindActiveByTokenHashAsync(string tokenHash, DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("SELECT id, user_id, expires_at, revoked_at FROM public.user_sessions WHERE token_hash=@hash AND revoked_at IS NULL AND expires_at>@now LIMIT 1", connection);
        command.Parameters.AddWithValue("hash", NpgsqlDbType.Char, tokenHash);
        command.Parameters.AddWithValue("now", NpgsqlDbType.TimestampTz, nowUtc.UtcDateTime);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new SessionRecord(reader.GetGuid(0), reader.GetGuid(1), new DateTimeOffset(reader.GetDateTime(2), TimeSpan.Zero), reader.IsDBNull(3) ? null : new DateTimeOffset(reader.GetDateTime(3), TimeSpan.Zero));
    }
    public async Task<bool> RevokeByTokenHashAsync(string tokenHash, DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("UPDATE public.user_sessions SET revoked_at=@now WHERE token_hash=@hash AND revoked_at IS NULL", connection);
        command.Parameters.AddWithValue("hash", NpgsqlDbType.Char, tokenHash);
        command.Parameters.AddWithValue("now", NpgsqlDbType.TimestampTz, nowUtc.UtcDateTime);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }
}
