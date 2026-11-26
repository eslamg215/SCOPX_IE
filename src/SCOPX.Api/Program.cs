using Microsoft.AspNetCore.Identity;
using SCOPX.Api.Security;
using SCOPX.Api.Authentication;
using SCOPX.Infrastructure.Users;
using SCOPX.Infrastructure.Sessions;
using SCOPX.Infrastructure.Hospitals;
using SCOPX.Api.Tenancy;
using SCOPX.Api.Media;
using SCOPX.Api.Reports;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 60 * 1024 * 1024);

// Development-only API scaffold. Authentication and hospital membership lookup
// are enabled when configured; clinical and platform routes remain deliberately disabled.
// Never connect this scaffold to real patient data.
builder.Services.AddHealthChecks();
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options => options.MultipartBodyLengthLimit = 60 * 1024 * 1024);
builder.Services.AddScoped<IPasswordHasher<AuthUser>, PasswordHasher<AuthUser>>();
builder.Services.AddScoped<PasswordHashService>();
// Media routes are intentionally not mapped. The encryption service is ready for
// the protected pipeline; uploads remain fail-closed until a real scanner is installed.
builder.Services.AddSingleton<MediaContentProtector>();
builder.Services.AddSingleton<IMediaMalwareScanner, ClamAvMediaMalwareScanner>();
builder.Services.AddSingleton<ReportSigningService>();
var databaseConnection = builder.Configuration.GetConnectionString("ScopxDb");
if (!string.IsNullOrWhiteSpace(databaseConnection))
{
    builder.Services.AddScoped<IUserRepository>(_ => new NpgsqlUserRepository(databaseConnection));
    builder.Services.AddScoped<ISessionRepository>(_ => new NpgsqlSessionRepository(databaseConnection));
    builder.Services.AddScoped<IHospitalMembershipRepository>(_ => new NpgsqlHospitalMembershipRepository(databaseConnection));
}
builder.Services.AddScopxRateLimiting();
builder.Services.AddScopxCors(builder.Configuration);

var app = builder.Build();
app.UseScopxExceptionHandling();
app.UseScopxSecurityHeaders();
app.UseCors("scopx");
app.UseRateLimiter();

app.MapGet("/", () => Results.Ok(new
{
    product = "SCOPX.I.E",
    service = "SCOPX API",
    status = "development scaffold",
    apiVersion = "v1",
    clinicalUse = false,
    note = "Not clinically validated. Do not use with real patient data."
})).RequireRateLimiting("api");

// Public build metadata only; never expose secrets, tenant records, or patient data here.
app.MapGet("/api/v1/system", () => Results.Ok(new
{
    product = "SCOPX.I.E",
    apiVersion = "v1",
    environment = app.Environment.EnvironmentName,
    status = "scaffold",
    features = new[] { "health-check", "system-metadata", "rate-limiting", "security-headers", "correlation-id", "password-hash-helper", "authentication", "hospital-membership-context" },
    clinicalApiEnabled = false,
    platformAdminApiEnabled = false,
    note = "Clinical and platform administration routes are withheld until audit logging and separate privileged access are implemented and tested."
})).RequireRateLimiting("api");

// Clinical and platform-administration routes remain deliberately disabled.
// They must not be exposed until append-only audit recording is wired into every
// sensitive operation and platform administration uses a separately restricted
// connection with independently tested authorization.
app.MapHealthChecks("/health");

// Readiness is distinct from liveness: this probe verifies only that the configured
// PostgreSQL connection can open and execute SELECT 1. It does not validate schema,
// migrations, tenant policies, or clinical workflows.
app.MapGet("/health/ready", async (CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(databaseConnection))
        return Results.Json(new { status = "not_ready", database = "not_configured" }, statusCode: StatusCodes.Status503ServiceUnavailable);

    try
    {
        await using var connection = new Npgsql.NpgsqlConnection(databaseConnection);
        await connection.OpenAsync(ct);
        await using var command = new Npgsql.NpgsqlCommand("SELECT 1", connection);
        await command.ExecuteScalarAsync(ct);
        return Results.Ok(new { status = "ready", database = "reachable" });
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested)
    {
        throw;
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning("Database readiness probe failed ({ExceptionType}).", ex.GetType().Name);
        return Results.Json(new { status = "not_ready", database = "unavailable" }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

if (!string.IsNullOrWhiteSpace(databaseConnection))
{
    app.MapAuthEndpoints();
    app.MapHospitalContextEndpoints();
}
else
{
    app.MapPost("/api/v1/auth/login", () => Results.StatusCode(StatusCodes.Status503ServiceUnavailable));
    app.MapGet("/api/v1/auth/me", () => Results.StatusCode(StatusCodes.Status503ServiceUnavailable));
    app.MapPost("/api/v1/auth/logout", () => Results.StatusCode(StatusCodes.Status503ServiceUnavailable));
}
app.Run();
