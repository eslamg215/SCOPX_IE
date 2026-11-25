using Npgsql;
using SCOPX.Api.Authentication;
using SCOPX.Infrastructure.Hospitals;
using SCOPX.Infrastructure.Reports;
using SCOPX.Infrastructure.Sessions;
using SCOPX.Infrastructure.Users;
namespace SCOPX.Api.Reports;
public static class ReportEndpoints
{
 public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder endpoints)
 {
  var g=endpoints.MapGroup("/api/v1/reports").RequireRateLimiting("api");
  g.MapGet("/procedure/{procedureId:guid}",async(Guid procedureId,HttpContext c,IUserRepository u,ISessionRepository s,IHospitalMembershipRepository m,IReportRepository r,CancellationToken ct)=>{var a=await Resolve(c,u,s,m,ct);if(a is null)return Results.Unauthorized();if(!CanRead(a.Roles))return Results.Forbid();return Results.Ok(await r.ListForProcedureAsync(a.HospitalId,procedureId,ct));});
  g.MapGet("/{id:guid}",async(Guid id,HttpContext c,IUserRepository u,ISessionRepository s,IHospitalMembershipRepository m,IReportRepository r,CancellationToken ct)=>{var a=await Resolve(c,u,s,m,ct);if(a is null)return Results.Unauthorized();if(!CanRead(a.Roles))return Results.Forbid();var row=await r.GetAsync(a.HospitalId,id,ct);return row is null?(IResult)Results.NotFound():Results.Ok(row);});
  g.MapPost("",async(ReportCreateRequest input,HttpContext c,IUserRepository u,ISessionRepository s,IHospitalMembershipRepository m,IReportRepository r,CancellationToken ct)=>{var a=await Resolve(c,u,s,m,ct);if(a is null)return Results.Unauthorized();if(!CanWrite(a.Roles))return Results.Forbid();if(input.ProcedureId==Guid.Empty||string.IsNullOrWhiteSpace(input.Body)||input.Body.Length>20000)return Results.BadRequest(new{error="invalid_report"});try{var row=await r.CreateDraftAsync(a.HospitalId,a.UserId,input.ProcedureId,input.Body.Trim(),ct);return row is null?(IResult)Results.NotFound(new{error="procedure_not_found"}):Results.Created($"/api/v1/reports/{row.Id}",row);}catch(PostgresException ex) when(ex.SqlState==PostgresErrorCodes.ForeignKeyViolation){return Results.NotFound(new{error="procedure_not_found"});}});
  g.MapPut("/{id:guid}",async(Guid id,ReportUpdateRequest input,HttpContext c,IUserRepository u,ISessionRepository s,IHospitalMembershipRepository m,IReportRepository r,CancellationToken ct)=>{var a=await Resolve(c,u,s,m,ct);if(a is null)return Results.Unauthorized();if(!CanWrite(a.Roles))return Results.Forbid();if(string.IsNullOrWhiteSpace(input.Body)||input.Body.Length>20000)return Results.BadRequest(new{error="invalid_report_body"});var updated=await r.UpdateDraftAsync(a.HospitalId,a.UserId,id,input.Body.Trim(),ct);if(!updated)return Results.Conflict(new{error="report_not_draft_or_not_found"});return Results.Ok(await r.GetAsync(a.HospitalId,id,ct));});
  g.MapPost("/{id:guid}/submit-review",async(Guid id,HttpContext c,IUserRepository u,ISessionRepository s,IHospitalMembershipRepository m,IReportRepository r,CancellationToken ct)=>{var a=await Resolve(c,u,s,m,ct);if(a is null)return Results.Unauthorized();if(!CanReview(a.Roles))return Results.Forbid();var ok=await r.SubmitReviewAsync(a.HospitalId,a.UserId,id,ct);return ok?(IResult)Results.Ok(new{status="review"}):Results.Conflict(new{error="report_not_ready_or_not_draft"});});
  return endpoints;
 }
 private static async Task<AuthContext?> Resolve(HttpContext c,IUserRepository u,ISessionRepository s,IHospitalMembershipRepository m,CancellationToken ct){var token=AuthHeaderReader.ReadBearer(c.Request.Headers.Authorization.ToString());if(token is null)return null;var sess=await s.FindActiveByTokenHashAsync(SessionToken.Hash(token),DateTimeOffset.UtcNow,ct);if(sess is null)return null;var user=await u.FindByIdAsync(sess.UserId,ct);if(user is not{Status:"active"})return null;if(!Guid.TryParse(c.Request.Headers["X-Hospital-ID"].ToString(),out var hid))return null;var member=await m.FindActiveForUserAndHospitalAsync(user.Id,hid,ct);return member is null?null:new(user.Id,hid,member.RoleCodes.ToArray());}
 private static bool CanRead(string[] roles)=>roles.Any(x=>new[]{"hospital_admin","doctor","nurse","auditor","endoscopy_technician"}.Contains(x,StringComparer.OrdinalIgnoreCase));
 private static bool CanWrite(string[] roles)=>roles.Any(x=>new[]{"hospital_admin","doctor"}.Contains(x,StringComparer.OrdinalIgnoreCase));
 private static bool CanReview(string[] roles)=>roles.Any(x=>new[]{"hospital_admin","doctor"}.Contains(x,StringComparer.OrdinalIgnoreCase));
 private sealed record AuthContext(Guid UserId,Guid HospitalId,string[] Roles);
}
