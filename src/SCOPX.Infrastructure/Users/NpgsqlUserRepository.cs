using Npgsql;
using NpgsqlTypes;

namespace SCOPX.Infrastructure.Users;


/// <summary>Parameterized PostgreSQL implementation. Caller supplies an already validated connection string.</summary>
public sealed class NpgsqlUserRepository(string connectionString) : IUserRepository
{
    private const string SelectColumns = "id, email_normalized, display_name, status, password_hash, failed_login_count, lockout_until, last_login_at";

    public async Task<UserRecord?> FindByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedEmail);
        var email = normalizedEmail.Trim().ToUpperInvariant();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand($"SELECT {SelectColumns} FROM public.users WHERE email_normalized = @email LIMIT 1", connection);
        command.Parameters.AddWithValue("email", NpgsqlDbType.Varchar, email);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadUser(reader) : null;
    }


    public async Task<UserRecord?> FindByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand($"SELECT {SelectColumns} FROM public.users WHERE id = @id LIMIT 1", connection);
        command.Parameters.AddWithValue("id", NpgsqlDbType.Uuid, userId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadUser(reader) : null;
    }


    public async Task<UserRecord?> IncrementFailedLoginAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        const string sql = @"UPDATE public.users SET
            failed_login_count = failed_login_count + 1,
            lockout_until = CASE
                WHEN failed_login_count + 1 >= 15 THEN now() + interval '24 hours'
                WHEN failed_login_count + 1 >= 10 THEN now() + interval '1 hour'
                WHEN failed_login_count + 1 >= 5 THEN now() + interval '15 minutes'
                ELSE NULL END,
            updated_at = now()
            WHERE id = @id AND status = 'active'
            RETURNING id, email_normalized, display_name, status, password_hash, failed_login_count, lockout_until, last_login_at";
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", NpgsqlDbType.Uuid, userId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadUser(reader) : null;
    }


    public async Task<bool> RecordFailedLoginAsync(Guid userId, int failedCount, DateTimeOffset? lockoutUntil, CancellationToken cancellationToken = default)
    {
        if (failedCount < 1) throw new ArgumentOutOfRangeException(nameof(failedCount));
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            "UPDATE public.users SET failed_login_count = @count, lockout_until = @lockout, updated_at = now() WHERE id = @id", connection);
        command.Parameters.AddWithValue("count", NpgsqlDbType.Integer, failedCount);
        command.Parameters.AddWithValue("lockout", NpgsqlDbType.TimestampTz, (object?)lockoutUntil?.UtcDateTime ?? DBNull.Value);
        command.Parameters.AddWithValue("id", NpgsqlDbType.Uuid, userId);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }


    public async Task<bool> RecordSuccessfulLoginAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            "UPDATE public.users SET failed_login_count = 0, lockout_until = NULL, last_login_at = now(), updated_at = now() WHERE id = @id AND status = 'active'", connection);
        command.Parameters.AddWithValue("id", NpgsqlDbType.Uuid, userId);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }


    private static UserRecord ReadUser(NpgsqlDataReader reader) => new(
        reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
        reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetInt32(5),
        reader.IsDBNull(6) ? null : new DateTimeOffset(reader.GetDateTime(6), TimeSpan.Zero),
        reader.IsDBNull(7) ? null : new DateTimeOffset(reader.GetDateTime(7), TimeSpan.Zero));
}
