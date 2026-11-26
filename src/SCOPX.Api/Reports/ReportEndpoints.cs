using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Npgsql;
using SCOPX.Api.Authentication;
using SCOPX.Api.Security;
using SCOPX.Infrastructure.Hospitals;
using SCOPX.Infrastructure.Reports;
using SCOPX.Infrastructure.Sessions;
using SCOPX.Infrastructure.Users;

namespace SCOPX.Api.Reports;

public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/reports").RequireRateLimiting("api");

        group.MapGet("/procedure/{procedureId:guid}",
            async (Guid procedureId, HttpContext context, IUserRepository users, ISessionRepository sessions,
                IHospitalMembershipRepository memberships, IReportRepository reports, CancellationToken ct) =>
            {
                var actor = await Resolve(context, users, sessions, memberships, ct);
                if (actor is null) return Results.Unauthorized();
                if (!CanRead(actor.Roles)) return Results.Forbid();
                return Results.Ok(await reports.ListForProcedureAsync(actor.HospitalId, actor.UserId, procedureId, ct));
            });

        group.MapGet("/{id:guid}",
            async (Guid id, HttpContext context, IUserRepository users, ISessionRepository sessions,
                IHospitalMembershipRepository memberships, IReportRepository reports, CancellationToken ct) =>
            {
                var actor = await Resolve(context, users, sessions, memberships, ct);
                if (actor is null) return Results.Unauthorized();
                if (!CanRead(actor.Roles)) return Results.Forbid();
                var row = await reports.GetAsync(actor.HospitalId, actor.UserId, id, ct);
                return row is null ? (IResult)Results.NotFound() : Results.Ok(row);
            });

        group.MapPost("",
            async (ReportCreateRequest input, HttpContext context, IUserRepository users, ISessionRepository sessions,
                IHospitalMembershipRepository memberships, IReportRepository reports, CancellationToken ct) =>
            {
                var actor = await Resolve(context, users, sessions, memberships, ct);
                if (actor is null) return Results.Unauthorized();
                if (!CanWrite(actor.Roles)) return Results.Forbid();
                if (input.ProcedureId == Guid.Empty || string.IsNullOrWhiteSpace(input.Body) || input.Body.Length > 20000)
                    return Results.BadRequest(new { error = "invalid_report" });

                try
                {
                    var row = await reports.CreateDraftAsync(actor.HospitalId, actor.UserId,
                        actor.MembershipId, input.ProcedureId, input.Body.Trim(), ct);
                    return row is null
                        ? (IResult)Results.NotFound(new { error = "procedure_not_found" })
                        : Results.Created($"/api/v1/reports/{row.Id}", row);
                }
                catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
                {
                    return Results.NotFound(new { error = "procedure_not_found" });
                }
            });

        group.MapPut("/{id:guid}",
            async (Guid id, ReportUpdateRequest input, HttpContext context, IUserRepository users,
                ISessionRepository sessions, IHospitalMembershipRepository memberships,
                IReportRepository reports, CancellationToken ct) =>
            {
                var actor = await Resolve(context, users, sessions, memberships, ct);
                if (actor is null) return Results.Unauthorized();
                if (!CanWrite(actor.Roles)) return Results.Forbid();
                if (string.IsNullOrWhiteSpace(input.Body) || input.Body.Length > 20000 || input.ExpectedVersion < 1)
                    return Results.BadRequest(new { error = "invalid_report_body_or_version" });

                var updated = await reports.UpdateDraftAsync(actor.HospitalId, actor.UserId,
                    id, input.ExpectedVersion, input.Body.Trim(), ct);
                if (!updated) return Results.Conflict(new { error = "report_version_conflict_or_not_editable" });
                return Results.Ok(await reports.GetAsync(actor.HospitalId, actor.UserId, id, ct));
            });

        group.MapPost("/{id:guid}/submit-review",
            async (Guid id, HttpContext context, IUserRepository users, ISessionRepository sessions,
                IHospitalMembershipRepository memberships, IReportRepository reports, CancellationToken ct) =>
            {
                var actor = await Resolve(context, users, sessions, memberships, ct);
                if (actor is null) return Results.Unauthorized();
                if (!CanReview(actor.Roles)) return Results.Forbid();
                var ok = await reports.SubmitReviewAsync(actor.HospitalId, actor.UserId, id, ct);
                return ok ? (IResult)Results.Ok(new { status = "review" })
                    : Results.Conflict(new { error = "report_not_ready_or_not_draft" });
            });

        // Implemented but intentionally not mapped by Program.cs until the clinical workflow
        // receives independent security, UI, and operational review.
        group.MapPost("/{id:guid}/sign",
            async (Guid id, ReportSignRequest input, HttpContext context, IUserRepository users,
                ISessionRepository sessions, IHospitalMembershipRepository memberships,
                IReportRepository reports, PasswordHashService passwordHashes,
                ReportSigningService signing, CancellationToken ct) =>
            {
                var actor = await Resolve(context, users, sessions, memberships, ct);
                if (actor is null) return Results.Unauthorized();
                if (!CanReview(actor.Roles)) return Results.Forbid();
                if (input.ExpectedVersion < 1 || string.IsNullOrWhiteSpace(input.Password) ||
                    input.Password.Length > PasswordPolicy.MaximumLength)
                    return Results.BadRequest(new { error = "invalid_signing_request" });

                var user = await users.FindByIdAsync(actor.UserId, ct);
                if (user is not { Status: "active" } || string.IsNullOrWhiteSpace(user.PasswordHash) ||
                    LoginLockoutPolicy.IsLocked(user.LockoutUntil, DateTimeOffset.UtcNow))
                    return Results.Json(new { error = "reauthentication_failed" }, statusCode: StatusCodes.Status401Unauthorized);

                var subject = new AuthUser { Id = user.Id.ToString("N") };
                if (passwordHashes.Verify(subject, user.PasswordHash, input.Password) == PasswordVerificationResult.Failed)
                {
                    await users.IncrementFailedLoginAsync(user.Id, ct);
                    return Results.Json(new { error = "reauthentication_failed" }, statusCode: StatusCodes.Status401Unauthorized);
                }

                var report = await reports.GetAsync(actor.HospitalId, actor.UserId, id, ct);
                if (report is null) return Results.NotFound();
                if (report.Status != "review" || report.Version != input.ExpectedVersion)
                    return Results.Conflict(new { error = "report_version_conflict_or_not_reviewed" });

                var reauthenticatedAt = DateTimeOffset.UtcNow;
                ReportSignatureArtifact artifact;
                try
                {
                    artifact = signing.Sign(new ReportSigningInput(
                        actor.HospitalId, report.Id, report.Version, report.BodySha256,
                        actor.MembershipId, reauthenticatedAt, reauthenticatedAt, "password"));
                }
                catch (Exception ex) when (ex is InvalidOperationException or CryptographicException)
                {
                    return Results.Json(new { error = "report_signing_unavailable" },
                        statusCode: StatusCodes.Status503ServiceUnavailable);
                }

                try
                {
                    var signed = await reports.SignAsync(actor.HospitalId, actor.UserId, actor.MembershipId,
                        report.Id, report.Version, artifact.Payload.DocumentSha256,
                        artifact.Payload.SignedAt, artifact.Payload.ReauthenticatedAt,
                        artifact.Payload.AuthenticationMethod, artifact.Payload.CanonicalizationVersion,
                        artifact.SignatureAlgorithm, artifact.Payload.SigningKeyId, artifact.SignatureValue, ct);
                    if (!signed)
                        return Results.Conflict(new { error = "report_version_conflict_or_not_reviewed" });
                }
                catch (PostgresException ex) when (
                    ex.SqlState == PostgresErrorCodes.UniqueViolation ||
                    ex.SqlState == PostgresErrorCodes.CheckViolation ||
                    ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
                {
                    return Results.Conflict(new { error = "report_version_conflict_or_not_reviewed" });
                }

                return Results.Ok(await reports.GetAsync(actor.HospitalId, actor.UserId, id, ct));
            }).RequireRateLimiting("login");

        return endpoints;
    }

    private static async Task<AuthContext?> Resolve(HttpContext context, IUserRepository users,
        ISessionRepository sessions, IHospitalMembershipRepository memberships, CancellationToken ct)
    {
        var token = AuthHeaderReader.ReadBearer(context.Request.Headers.Authorization.ToString());
        if (token is null) return null;
        var session = await sessions.FindActiveByTokenHashAsync(SessionToken.Hash(token), DateTimeOffset.UtcNow, ct);
        if (session is null) return null;
        var user = await users.FindByIdAsync(session.UserId, ct);
        if (user is not { Status: "active" }) return null;
        if (!Guid.TryParse(context.Request.Headers["X-Hospital-ID"].ToString(), out var hospitalId)) return null;
        var membership = await memberships.FindActiveForUserAndHospitalAsync(user.Id, hospitalId, ct);
        return membership is null
            ? null
            : new AuthContext(user.Id, membership.MembershipId, hospitalId, membership.RoleCodes.ToArray());
    }

    private static bool CanRead(string[] roles) =>
        roles.Any(role => new[] { "hospital_admin", "doctor", "nurse", "auditor", "endoscopy_technician" }
            .Contains(role, StringComparer.OrdinalIgnoreCase));

    private static bool CanWrite(string[] roles) =>
        roles.Contains("doctor", StringComparer.OrdinalIgnoreCase);

    private static bool CanReview(string[] roles) =>
        roles.Contains("doctor", StringComparer.OrdinalIgnoreCase);

    private sealed record AuthContext(Guid UserId, Guid MembershipId, Guid HospitalId, string[] Roles);
}

public sealed record ReportSignRequest(int ExpectedVersion, string Password);
