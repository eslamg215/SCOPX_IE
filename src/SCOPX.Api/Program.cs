using Microsoft.AspNetCore.Identity;
using SCOPX.Api.Security;
using SCOPX.Api.Authentication;
using SCOPX.Infrastructure.Users;
using SCOPX.Infrastructure.Sessions;
using SCOPX.Infrastructure.Hospitals;
using SCOPX.Api.Tenancy;
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
var databaseConnection = builder.Configuration.GetConnectionString("ScopxDb");
if (!string.IsNullOrWhiteSpace(databaseConnection))
{
    builder.Services.AddScoped<IUserRepository>(_ => new NpgsqlUserRepository(databaseConnection));
    builder.Services.AddScoped<ISessionRepository>(_ => new NpgsqlSessionRepository(databaseConnection));
    builder.Services.AddScoped<IHospitalMembershipRepository>(_ => new NpgsqlHospitalMembershipRepository(databaseConnection));
}
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // Partition by the socket peer address so one client cannot consume the
    // entire development API's shared rate-limit bucket. Production deployments
    // behind a proxy must configure trusted forwarded headers before relying on this key.
    options.AddPolicy("api", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    // Password guessing is more sensitive than ordinary API reads.
    options.AddPolicy("login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

var app = builder.Build();
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    context.Response.ContentType = "application/json";
    await context.Response.WriteAsJsonAsync(new { error = "internal_error", message = "An unexpected error occurred." });
}));
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    // Accept a caller correlation ID only if it is short and safe; otherwise generate one.
    var incoming = context.Request.Headers["X-Correlation-ID"].ToString();
    var correlationId = incoming.Length is > 0 and <= 64 && incoming.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_')
        ? incoming
        : Guid.NewGuid().ToString("N");
    context.TraceIdentifier = correlationId;
    context.Response.Headers["X-Correlation-ID"] = correlationId;
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Cache-Control"] = "no-store";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
    await next();
});

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
app.MapHealthChecks("/health").RequireRateLimiting("api");
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
