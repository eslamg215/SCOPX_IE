using System.Net.Mail;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Npgsql;
using NpgsqlTypes;
using SCOPX.Api.Security;

if (!args.Contains("--dev-only", StringComparer.Ordinal))
{
    Console.Error.WriteLine("Refusing to run without the explicit --dev-only switch.");
    return 2;
}

var database = Environment.GetEnvironmentVariable("POSTGRES_DB");
var owner = Environment.GetEnvironmentVariable("POSTGRES_USER");
var ownerPassword = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD");
if (database != "scopx_dev" || string.IsNullOrWhiteSpace(owner) || owner == "scopx_runtime" || string.IsNullOrWhiteSpace(ownerPassword))
{
    Console.Error.WriteLine("This bootstrap tool is restricted to scopx_dev and the local database-owner credentials.");
    return 2;
}

Console.Write("Hospital name: ");
var hospitalName = (Console.ReadLine() ?? string.Empty).Trim();
Console.Write("Administrator display name: ");
var displayName = (Console.ReadLine() ?? string.Empty).Trim();
Console.Write("Administrator email: ");
var emailInput = (Console.ReadLine() ?? string.Empty).Trim();
if (hospitalName.Length is < 2 or > 200 || displayName.Length is < 2 or > 160 || emailInput.Length is < 3 or > 320)
{
    Console.Error.WriteLine("Hospital, display name, or email length is invalid.");
    return 2;
}
string normalizedEmail;
try { normalizedEmail = new MailAddress(emailInput).Address.ToUpperInvariant(); }
catch { Console.Error.WriteLine("Enter a valid email address."); return 2; }

var password = ReadHidden("Create administrator password: ");
var confirm = ReadHidden("Confirm administrator password: ");
if (!string.Equals(password, confirm, StringComparison.Ordinal))
{
    Console.Error.WriteLine("Passwords do not match.");
    return 2;
}
if (!PasswordPolicy.IsAcceptable(password))
{
    Console.Error.WriteLine($"Password must be {PasswordPolicy.MinimumLength}-{PasswordPolicy.MaximumLength} characters and contain uppercase, lowercase, and numeric characters.");
    return 2;
}

var userId = Guid.NewGuid();
var hospitalId = Guid.NewGuid();
var authUser = new AuthUser { Id = userId.ToString("N") };
var passwordHash = new PasswordHasher<AuthUser>().HashPassword(authUser, password);
password = string.Empty;
confirm = string.Empty;

var connectionString = new NpgsqlConnectionStringBuilder
{
    Host = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "localhost",
    Port = 5432,
    Database = database,
    Username = owner,
    Password = ownerPassword,
    IncludeErrorDetail = false,
    Timeout = 10
}.ConnectionString;

await using var connection = new NpgsqlConnection(connectionString);
await connection.OpenAsync();
await using var transaction = await connection.BeginTransactionAsync();

await using (var check = new NpgsqlCommand("SELECT (SELECT count(*) FROM public.users), (SELECT count(*) FROM public.hospitals)", connection, transaction))
await using (var reader = await check.ExecuteReaderAsync())
{
    await reader.ReadAsync();
    if (reader.GetInt64(0) != 0 || reader.GetInt64(1) != 0)
    {
        Console.Error.WriteLine("Bootstrap requires an empty users and hospitals database. No changes were made.");
        return 2;
    }
}

await using (var userCommand = new NpgsqlCommand(@"
INSERT INTO public.users (id, email_normalized, display_name, status, password_hash)
VALUES (@id, @email, @display, 'active', @hash);", connection, transaction))
{
    userCommand.Parameters.AddWithValue("id", NpgsqlDbType.Uuid, userId);
    userCommand.Parameters.AddWithValue("email", NpgsqlDbType.Varchar, normalizedEmail);
    userCommand.Parameters.AddWithValue("display", NpgsqlDbType.Varchar, displayName);
    userCommand.Parameters.AddWithValue("hash", NpgsqlDbType.Text, passwordHash);
    await userCommand.ExecuteNonQueryAsync();
}

await using (var hospitalCommand = new NpgsqlCommand(@"
INSERT INTO public.hospitals (id, name, status)
VALUES (@id, @name, 'active');", connection, transaction))
{
    hospitalCommand.Parameters.AddWithValue("id", NpgsqlDbType.Uuid, hospitalId);
    hospitalCommand.Parameters.AddWithValue("name", NpgsqlDbType.Varchar, hospitalName);
    await hospitalCommand.ExecuteNonQueryAsync();
}

var membershipId = Guid.NewGuid();
await using (var membershipCommand = new NpgsqlCommand(@"
INSERT INTO public.hospital_memberships (id, hospital_id, user_id, status)
VALUES (@id, @hospital, @user, 'active');", connection, transaction))
{
    membershipCommand.Parameters.AddWithValue("id", NpgsqlDbType.Uuid, membershipId);
    membershipCommand.Parameters.AddWithValue("hospital", NpgsqlDbType.Uuid, hospitalId);
    membershipCommand.Parameters.AddWithValue("user", NpgsqlDbType.Uuid, userId);
    await membershipCommand.ExecuteNonQueryAsync();
}

await using (var roleCommand = new NpgsqlCommand(@"
INSERT INTO public.membership_roles (hospital_id, membership_id, role_id)
SELECT @hospital, @membership, id FROM public.roles
WHERE code = 'hospital_admin' AND scope = 'hospital';", connection, transaction))
{
    roleCommand.Parameters.AddWithValue("hospital", NpgsqlDbType.Uuid, hospitalId);
    roleCommand.Parameters.AddWithValue("membership", NpgsqlDbType.Uuid, membershipId);
    if (await roleCommand.ExecuteNonQueryAsync() != 1)
        throw new InvalidOperationException("Hospital Admin role is missing. Apply migrations first.");
}

await using (var auditCommand = new NpgsqlCommand(@"
INSERT INTO public.audit_events (hospital_id, actor_user_id, event_type, target_type, target_id, details)
VALUES (@hospital, @actor, 'bootstrap.admin_created', 'user', @target, @details);", connection, transaction))
{
    auditCommand.Parameters.AddWithValue("hospital", NpgsqlDbType.Uuid, hospitalId);
    auditCommand.Parameters.AddWithValue("actor", NpgsqlDbType.Uuid, userId);
    auditCommand.Parameters.AddWithValue("target", NpgsqlDbType.Text, userId.ToString("D"));
    auditCommand.Parameters.AddWithValue("details", NpgsqlDbType.Jsonb, "{\"source\":\"dev_bootstrap\"}");
    await auditCommand.ExecuteNonQueryAsync();
}

await transaction.CommitAsync();
Console.WriteLine("Development hospital administrator created successfully.");
Console.WriteLine($"Hospital ID: {hospitalId:D}");
Console.WriteLine($"User ID: {userId:D}");
Console.WriteLine("The password was stored as an ASP.NET Core password hash; the plaintext was not stored or displayed.");
Console.WriteLine("This account has hospital_admin only. Assign doctor roles only to verified test accounts through a reviewed provisioning workflow.");
return 0;

static string ReadHidden(string prompt)
{
    Console.Write(prompt);
    var value = new StringBuilder();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter) { Console.WriteLine(); break; }
        if (key.Key == ConsoleKey.Backspace && value.Length > 0) { value.Length--; continue; }
        if (!char.IsControl(key.KeyChar)) value.Append(key.KeyChar);
    }
    return value.ToString();
}
