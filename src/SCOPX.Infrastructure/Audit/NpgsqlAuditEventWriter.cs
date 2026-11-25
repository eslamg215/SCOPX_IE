using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace SCOPX.Infrastructure.Audit;

/// <summary>
/// Writes append-only audit events inside the caller's existing database transaction.
/// Details must contain operational metadata only; never include names, notes, report text,
/// image/video content, passwords, bearer tokens, or other patient-identifying payloads.
/// </summary>
public static class NpgsqlAuditEventWriter
{
    public static async Task WriteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid hospitalId,
        Guid actorUserId,
        string eventType,
        string targetType,
        Guid targetId,
        object details,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(details);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetType);

        const string sql = @"
INSERT INTO public.audit_events
    (hospital_id, actor_user_id, event_type, target_type, target_id, details)
VALUES
    (@hospital, @actor, @event, @target, @target_id, @details);";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("hospital", NpgsqlDbType.Uuid, hospitalId);
        command.Parameters.AddWithValue("actor", NpgsqlDbType.Uuid, actorUserId);
        command.Parameters.AddWithValue("event", NpgsqlDbType.Varchar, eventType);
        command.Parameters.AddWithValue("target", NpgsqlDbType.Varchar, targetType);
        command.Parameters.AddWithValue("target_id", NpgsqlDbType.Text, targetId.ToString("D"));
        command.Parameters.AddWithValue("details", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(details));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
