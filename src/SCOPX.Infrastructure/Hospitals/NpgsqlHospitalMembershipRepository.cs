using Npgsql;
using NpgsqlTypes;

namespace SCOPX.Infrastructure.Hospitals;

/// <summary>Membership lookup using transaction-local user/tenant settings required by optional RLS policies.</summary>
public sealed class NpgsqlHospitalMembershipRepository(string connectionString) : IHospitalMembershipRepository
{
    private static async Task SetContextAsync(NpgsqlConnection connection, NpgsqlTransaction tx, Guid userId, Guid? hospitalId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("SELECT set_config('scopx.user_id', @user, true)", connection, tx);
        command.Parameters.AddWithValue("user", NpgsqlDbType.Text, userId.ToString());
        await command.ExecuteNonQueryAsync(ct);
        if (hospitalId is not null)
        {
            await using var tenantCommand = new NpgsqlCommand("SELECT set_config('scopx.hospital_id', @hospital, true)", connection, tx);
            tenantCommand.Parameters.AddWithValue("hospital", NpgsqlDbType.Text, hospitalId.Value.ToString());
            await tenantCommand.ExecuteNonQueryAsync(ct);
        }
    }


    public async Task<IReadOnlyList<HospitalMembershipRecord>> ListActiveForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        const string membershipsSql = @"
SELECT h.id, h.name, hm.status, hm.id
FROM public.hospital_memberships hm
JOIN public.hospitals h ON h.id = hm.hospital_id
WHERE hm.user_id = @user AND hm.status = 'active' AND h.status = 'active'
ORDER BY h.name;";
        const string rolesSql = @"
SELECT r.code
FROM public.membership_roles mr
JOIN public.roles r ON r.id = mr.role_id AND r.scope = 'hospital'
WHERE mr.hospital_id = @hospital AND mr.membership_id = @membership
ORDER BY r.code;";

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken);
        await SetContextAsync(connection, tx, userId, null, cancellationToken);
        await using var command = new NpgsqlCommand(membershipsSql, connection, tx);
        command.Parameters.AddWithValue("user", NpgsqlDbType.Uuid, userId);
        var memberships = new List<(Guid HospitalId, string HospitalName, string Status, Guid MembershipId)>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
                memberships.Add((reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetGuid(3)));
        }

        var rows = new List<HospitalMembershipRecord>(memberships.Count);
        foreach (var membership in memberships)
        {
            // Hospital context is derived from a membership row returned by the trusted DB query,
            // not from an untrusted request header. This permits role lookup under tenant RLS.
            await SetContextAsync(connection, tx, userId, membership.HospitalId, cancellationToken);
            await using var roleCommand = new NpgsqlCommand(rolesSql, connection, tx);
            roleCommand.Parameters.AddWithValue("hospital", NpgsqlDbType.Uuid, membership.HospitalId);
            roleCommand.Parameters.AddWithValue("membership", NpgsqlDbType.Uuid, membership.MembershipId);
            var roleCodes = new List<string>();
            await using var roleReader = await roleCommand.ExecuteReaderAsync(cancellationToken);
            while (await roleReader.ReadAsync(cancellationToken)) roleCodes.Add(roleReader.GetString(0));
            rows.Add(new HospitalMembershipRecord(membership.HospitalId, membership.HospitalName, membership.Status, membership.MembershipId, roleCodes));
        }
        await tx.CommitAsync(cancellationToken);
        return rows;
    }

    public async Task<HospitalMembershipRecord?> FindActiveForUserAndHospitalAsync(Guid userId, Guid hospitalId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT h.id, h.name, hm.status, hm.id, COALESCE(array_agg(DISTINCT r.code) FILTER (WHERE r.code IS NOT NULL), ARRAY[]::varchar[]) AS role_codes
FROM public.hospital_memberships hm
JOIN public.hospitals h ON h.id = hm.hospital_id
LEFT JOIN public.membership_roles mr ON mr.hospital_id = hm.hospital_id AND mr.membership_id = hm.id
LEFT JOIN public.roles r ON r.id = mr.role_id AND r.scope = 'hospital'
WHERE hm.user_id = @user AND hm.hospital_id = @hospital AND hm.status = 'active' AND h.status = 'active'
GROUP BY h.id, h.name, hm.status, hm.id
LIMIT 1;";
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken);
        await SetContextAsync(connection, tx, userId, hospitalId, cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection, tx);
        command.Parameters.AddWithValue("user", NpgsqlDbType.Uuid, userId);
        command.Parameters.AddWithValue("hospital", NpgsqlDbType.Uuid, hospitalId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        HospitalMembershipRecord? record = null;
        if (await reader.ReadAsync(cancellationToken))
            record = new HospitalMembershipRecord(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetGuid(3), reader.GetFieldValue<string[]>(4));
        await reader.CloseAsync();
        await tx.CommitAsync(cancellationToken);
        return record;
    }
}
