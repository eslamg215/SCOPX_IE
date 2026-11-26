using System.Security.Cryptography;
using System.Text;
using Npgsql;
using NpgsqlTypes;
using SCOPX.Infrastructure.Audit;

namespace SCOPX.Infrastructure.Reports;

public sealed class NpgsqlReportRepository(string connectionString) : IReportRepository
{
    private const string Columns = "id,hospital_id,procedure_id,report_version,status,report_body,created_at,updated_at";

    private static async Task SetTenant(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid hospitalId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(
            "SELECT set_config('scopx.hospital_id', @hospital, true)", connection, transaction);
        command.Parameters.AddWithValue("hospital", NpgsqlDbType.Text, hospitalId.ToString());
        await command.ExecuteNonQueryAsync(ct);
    }

    private static ProcedureReportRecord Read(NpgsqlDataReader reader) =>
        new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetInt32(3),
            reader.GetString(4), reader.GetString(5),
            reader.GetFieldValue<DateTimeOffset>(6), reader.GetFieldValue<DateTimeOffset>(7));

    // Canonical text representation used for the persisted SHA-256 digest.
    // The same canonicalization version must be used by any future signing service.
    private static string HashBody(string body)
    {
        var canonical = body.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Normalize(NormalizationForm.FormC);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    public async Task<IReadOnlyList<ProcedureReportRecord>> ListForProcedureAsync(
        Guid hospitalId, Guid actorUserId, Guid procedureId, CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenant(connection, transaction, hospitalId, ct);

        await using var command = new NpgsqlCommand(
            $"SELECT {Columns} FROM procedure_reports WHERE hospital_id=@hospital AND procedure_id=@procedure ORDER BY report_version DESC LIMIT 100",
            connection, transaction);
        command.Parameters.AddWithValue("hospital", hospitalId);
        command.Parameters.AddWithValue("procedure", procedureId);

        var rows = new List<ProcedureReportRecord>();
        await using (var reader = await command.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct)) rows.Add(Read(reader));

        await NpgsqlAuditEventWriter.WriteAsync(connection, transaction, hospitalId, actorUserId,
            "report.listed", "report_collection", procedureId, new { result_count = rows.Count }, ct);
        await transaction.CommitAsync(ct);
        return rows;
    }

    public async Task<ProcedureReportRecord?> GetAsync(
        Guid hospitalId, Guid actorUserId, Guid reportId, CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenant(connection, transaction, hospitalId, ct);

        await using var command = new NpgsqlCommand(
            $"SELECT {Columns} FROM procedure_reports WHERE hospital_id=@hospital AND id=@id LIMIT 1",
            connection, transaction);
        command.Parameters.AddWithValue("hospital", hospitalId);
        command.Parameters.AddWithValue("id", reportId);

        ProcedureReportRecord? row;
        await using (var reader = await command.ExecuteReaderAsync(ct))
            row = await reader.ReadAsync(ct) ? Read(reader) : null;

        await NpgsqlAuditEventWriter.WriteAsync(connection, transaction, hospitalId, actorUserId,
            "report.read", "procedure_report", reportId, new { found = row is not null }, ct);
        await transaction.CommitAsync(ct);
        return row;
    }

    public async Task<ProcedureReportRecord?> CreateDraftAsync(
        Guid hospitalId, Guid actorUserId, Guid actorMembershipId, Guid procedureId,
        string body, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO procedure_reports
                (hospital_id, procedure_id, created_by_membership_id, report_body, report_body_sha256, status)
            SELECT @hospital, id, @membership, @body, @sha, 'draft'
            FROM procedures
            WHERE hospital_id=@hospital AND id=@procedure
            RETURNING id;
            """;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenant(connection, transaction, hospitalId, ct);

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("hospital", hospitalId);
        command.Parameters.AddWithValue("procedure", procedureId);
        command.Parameters.AddWithValue("membership", actorMembershipId);
        command.Parameters.AddWithValue("body", NpgsqlDbType.Text, body);
        command.Parameters.AddWithValue("sha", NpgsqlDbType.Varchar, HashBody(body));

        var result = await command.ExecuteScalarAsync(ct);
        if (result is not Guid reportId)
        {
            await transaction.CommitAsync(ct);
            return null;
        }

        await NpgsqlAuditEventWriter.WriteAsync(connection, transaction, hospitalId, actorUserId,
            "report.draft_created", "procedure_report", reportId, new { operation = "create_draft" }, ct);
        await transaction.CommitAsync(ct);
        return await GetAsync(hospitalId, actorUserId, reportId, ct);
    }

    public async Task<bool> UpdateDraftAsync(
        Guid hospitalId, Guid actorUserId, Guid reportId, int expectedVersion, string body, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE procedure_reports
            SET report_body=@body,
                report_body_sha256=@sha,
                report_version=report_version+1,
                updated_at=now()
            WHERE hospital_id=@hospital AND id=@id AND report_version=@expected AND status='draft' AND signed_at IS NULL;
            """;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenant(connection, transaction, hospitalId, ct);

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("hospital", hospitalId);
        command.Parameters.AddWithValue("id", reportId);
        command.Parameters.AddWithValue("expected", expectedVersion);
        command.Parameters.AddWithValue("body", NpgsqlDbType.Text, body);
        command.Parameters.AddWithValue("sha", NpgsqlDbType.Varchar, HashBody(body));

        var affected = await command.ExecuteNonQueryAsync(ct);
        if (affected == 1)
            await NpgsqlAuditEventWriter.WriteAsync(connection, transaction, hospitalId, actorUserId,
                "report.draft_updated", "procedure_report", reportId, new { operation = "update_draft" }, ct);

        await transaction.CommitAsync(ct);
        return affected == 1;
    }

    public async Task<bool> SubmitReviewAsync(
        Guid hospitalId, Guid actorUserId, Guid reportId, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE procedure_reports
            SET status='review', updated_at=now()
            WHERE hospital_id=@hospital AND id=@id AND status='draft'
              AND length(trim(report_body))>0 AND signed_at IS NULL;
            """;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenant(connection, transaction, hospitalId, ct);

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("hospital", hospitalId);
        command.Parameters.AddWithValue("id", reportId);

        var affected = await command.ExecuteNonQueryAsync(ct);
        if (affected == 1)
            await NpgsqlAuditEventWriter.WriteAsync(connection, transaction, hospitalId, actorUserId,
                "report.submitted_for_review", "procedure_report", reportId, new { operation = "submit_review" }, ct);

        await transaction.CommitAsync(ct);
        return affected == 1;
    }
}
