using Npgsql;
using NpgsqlTypes;

namespace SCOPX.Infrastructure.Patients;


/// <summary>Tenant-scoped PostgreSQL patient data access. Every method sets transaction-local tenant context.</summary>
public sealed class NpgsqlPatientRepository(string connectionString) : IPatientRepository
{
    private static async Task SetTenantAsync(NpgsqlConnection c, NpgsqlTransaction tx, Guid hospitalId, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("SELECT set_config('scopx.hospital_id', @hospital, true)", c, tx);
        cmd.Parameters.AddWithValue("hospital", NpgsqlDbType.Text, hospitalId.ToString());
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<PatientListItem>> SearchAsync(Guid hospitalId, string? query, int limit, int offset, CancellationToken ct = default)
    {
        const string sql = @"SELECT id, patient_code, full_name, date_of_birth, sex_at_birth, contact_phone, status, created_at
FROM patients WHERE hospital_id=@hospital AND status='active'
AND (@q IS NULL OR full_name ILIKE @pattern OR patient_code ILIKE @pattern)
ORDER BY full_name, id LIMIT @limit OFFSET @offset";
        await using var c = new NpgsqlConnection(connectionString); await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct); await SetTenantAsync(c, tx, hospitalId, ct);
        await using var cmd = new NpgsqlCommand(sql, c, tx);
        cmd.Parameters.AddWithValue("hospital", NpgsqlDbType.Uuid, hospitalId);
        cmd.Parameters.AddWithValue("q", NpgsqlDbType.Text, (object?)query ?? DBNull.Value);
        cmd.Parameters.AddWithValue("pattern", NpgsqlDbType.Text, "%" + (query ?? "") + "%");
        cmd.Parameters.AddWithValue("limit", NpgsqlDbType.Integer, limit);
        cmd.Parameters.AddWithValue("offset", NpgsqlDbType.Integer, offset);
        await using var rd = await cmd.ExecuteReaderAsync(ct); var rows = new List<PatientListItem>();
        while (await rd.ReadAsync(ct)) rows.Add(new(rd.GetGuid(0), rd.GetString(1), rd.GetString(2), rd.IsDBNull(3)?null:DateOnly.FromDateTime(rd.GetDateTime(3)), rd.IsDBNull(4)?null:rd.GetString(4), rd.IsDBNull(5)?null:rd.GetString(5), rd.GetString(6), rd.GetFieldValue<DateTimeOffset>(7)));
        await tx.CommitAsync(ct); return rows;
    }

    public async Task<PatientRecord?> GetAsync(Guid hospitalId, Guid patientId, CancellationToken ct = default)
    {
        const string sql = @"SELECT id,hospital_id,patient_code,full_name,date_of_birth,sex_at_birth,contact_phone,notes,status,created_at,updated_at,archived_at
FROM patients WHERE hospital_id=@hospital AND id=@id LIMIT 1";
        await using var c = new NpgsqlConnection(connectionString); await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct); await SetTenantAsync(c, tx, hospitalId, ct);
        await using var cmd = new NpgsqlCommand(sql,c,tx); cmd.Parameters.AddWithValue("hospital",hospitalId); cmd.Parameters.AddWithValue("id",patientId);
        await using var rd = await cmd.ExecuteReaderAsync(ct); PatientRecord? p=null;
        if(await rd.ReadAsync(ct)) p=new(rd.GetGuid(0),rd.GetGuid(1),rd.GetString(2),rd.GetString(3),rd.IsDBNull(4)?null:DateOnly.FromDateTime(rd.GetDateTime(4)),rd.IsDBNull(5)?null:rd.GetString(5),rd.IsDBNull(6)?null:rd.GetString(6),rd.IsDBNull(7)?null:rd.GetString(7),rd.GetString(8),rd.GetFieldValue<DateTimeOffset>(9),rd.GetFieldValue<DateTimeOffset>(10),rd.IsDBNull(11)?null:rd.GetFieldValue<DateTimeOffset>(11));
        await tx.CommitAsync(ct); return p;
    }

    public async Task<PatientRecord?> CreateAsync(Guid hospitalId, PatientCreateRequest input, CancellationToken ct = default)
    {
        const string sql = @"INSERT INTO patients(hospital_id,patient_code,full_name,date_of_birth,sex_at_birth,contact_phone,notes)
VALUES(@hospital,@code,@name,@dob,@sex,@phone,@notes) RETURNING id";
        await using var c = new NpgsqlConnection(connectionString); await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct); await SetTenantAsync(c,tx,hospitalId,ct);
        await using var cmd = new NpgsqlCommand(sql,c,tx);
        cmd.Parameters.AddWithValue("hospital",hospitalId); cmd.Parameters.AddWithValue("code",input.PatientCode.Trim()); cmd.Parameters.AddWithValue("name",input.FullName.Trim());
        cmd.Parameters.AddWithValue("dob",NpgsqlDbType.Date,(object?)input.DateOfBirth ?? DBNull.Value); cmd.Parameters.AddWithValue("sex",NpgsqlDbType.Varchar,(object?)input.SexAtBirth ?? DBNull.Value);
        cmd.Parameters.AddWithValue("phone",NpgsqlDbType.Varchar,(object?)input.ContactPhone ?? DBNull.Value); cmd.Parameters.AddWithValue("notes",NpgsqlDbType.Text,(object?)input.Notes ?? DBNull.Value);
        var id=(Guid)(await cmd.ExecuteScalarAsync(ct) ?? throw new InvalidOperationException("Patient insert did not return ID")); await tx.CommitAsync(ct);
        return await GetAsync(hospitalId,id,ct);
    }

    public async Task<bool> UpdateAsync(Guid hospitalId, Guid patientId, PatientUpdateRequest input, CancellationToken ct = default)
    {
        const string sql = @"UPDATE patients SET full_name=@name,date_of_birth=@dob,sex_at_birth=@sex,contact_phone=@phone,notes=@notes,updated_at=now()
WHERE hospital_id=@hospital AND id=@id AND status='active'";
        await using var c=new NpgsqlConnection(connectionString); await c.OpenAsync(ct); await using var tx=await c.BeginTransactionAsync(ct); await SetTenantAsync(c,tx,hospitalId,ct);
        await using var cmd=new NpgsqlCommand(sql,c,tx); cmd.Parameters.AddWithValue("hospital",hospitalId); cmd.Parameters.AddWithValue("id",patientId); cmd.Parameters.AddWithValue("name",input.FullName.Trim());
        cmd.Parameters.AddWithValue("dob",NpgsqlDbType.Date,(object?)input.DateOfBirth ?? DBNull.Value); cmd.Parameters.AddWithValue("sex",NpgsqlDbType.Varchar,(object?)input.SexAtBirth ?? DBNull.Value); cmd.Parameters.AddWithValue("phone",NpgsqlDbType.Varchar,(object?)input.ContactPhone ?? DBNull.Value); cmd.Parameters.AddWithValue("notes",NpgsqlDbType.Text,(object?)input.Notes ?? DBNull.Value);
        var count=await cmd.ExecuteNonQueryAsync(ct); await tx.CommitAsync(ct); return count==1;
    }

    public async Task<bool> ArchiveAsync(Guid hospitalId, Guid patientId, CancellationToken ct = default)
    {
        const string sql="UPDATE patients SET status='archived',archived_at=now(),updated_at=now() WHERE hospital_id=@hospital AND id=@id AND status='active'";
        await using var c=new NpgsqlConnection(connectionString); await c.OpenAsync(ct); await using var tx=await c.BeginTransactionAsync(ct); await SetTenantAsync(c,tx,hospitalId,ct);
        await using var cmd=new NpgsqlCommand(sql,c,tx); cmd.Parameters.AddWithValue("hospital",hospitalId); cmd.Parameters.AddWithValue("id",patientId);
        var count=await cmd.ExecuteNonQueryAsync(ct); await tx.CommitAsync(ct); return count==1;
    }
}
