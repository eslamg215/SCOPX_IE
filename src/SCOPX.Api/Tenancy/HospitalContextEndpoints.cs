using SCOPX.Infrastructure.Hospitals;
using SCOPX.Infrastructure.Sessions;
using SCOPX.Infrastructure.Users;
using SCOPX.Api.Authentication;

namespace SCOPX.Api.Tenancy;


public static class HospitalContextEndpoints
{
    public static IEndpointRouteBuilder MapHospitalContextEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/hospitals").RequireRateLimiting("api");
        group.MapGet("/mine", async (HttpContext context, IUserRepository users, ISessionRepository sessions, IHospitalMembershipRepository memberships, CancellationToken ct) =>
        {
            var userId = await ResolveUserIdAsync(context, users, sessions, ct);
            if (userId is null) return Results.Unauthorized();
            var hospitals = await memberships.ListActiveForUserAsync(userId.Value, ct);
            return Results.Ok(hospitals.Select(h => new { hospital_id = h.HospitalId, hospital_name = h.HospitalName, roles = h.RoleCodes }));
        });
        group.MapGet("/context", async (HttpContext context, IUserRepository users, ISessionRepository sessions, IHospitalMembershipRepository memberships, CancellationToken ct) =>
        {
            var userId = await ResolveUserIdAsync(context, users, sessions, ct);
            if (userId is null) return Results.Unauthorized();
            var header = context.Request.Headers["X-Hospital-ID"].ToString();
            if (!Guid.TryParse(header, out var hospitalId)) return Results.BadRequest(new { error = "hospital_context_required" });
            var membership = await memberships.FindActiveForUserAndHospitalAsync(userId.Value, hospitalId, ct);
            if (membership is null) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return Results.Ok(new { hospital_id = membership.HospitalId, hospital_name = membership.HospitalName, roles = membership.RoleCodes });
        });
        return endpoints;
    }

    private static async Task<Guid?> ResolveUserIdAsync(HttpContext context, IUserRepository users, ISessionRepository sessions, CancellationToken ct)
    {
        var bearer = AuthHeaderReader.ReadBearer(context.Request.Headers.Authorization.ToString());
        if (bearer is null) return null;
        var session = await sessions.FindActiveByTokenHashAsync(SessionToken.Hash(bearer), DateTimeOffset.UtcNow, ct);
        if (session is null) return null;
        var user = await users.FindByIdAsync(session.UserId, ct);
        return user is { Status: "active" } ? user.Id : null;
    }
}
