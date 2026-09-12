using System.Security.Claims;
using CreditCardApplication.Api.Auth;
using CreditCardApplication.Application.Auth;
using CreditCardApplication.Application.Auditing;
using CreditCardApplication.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace CreditCardApplication.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(AuthService authService, JwtTokenService jwtTokenService,
    ApplicationDbContext db, AuditService auditService) : ControllerBase
{
    [EnableRateLimiting("login")]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return Ok(jwtTokenService.Create(result));
    }

    [EnableRateLimiting("login")]
    [HttpPost("report-login-issue")]
    public async Task<IActionResult> ReportLoginIssue(
        ReportLoginIssueRequest request,
        CancellationToken cancellationToken)
    {
        await authService.ReportLoginIssueAsync(
            request, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return NoContent();
    }

    [Authorize(Roles = "Officer,Manager")]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized();
        var sessionId = User.FindFirstValue("sid");
        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is not null && user.ActiveSessionId == sessionId)
        {
            user.ActiveSessionId = null;
            user.ActiveSessionExpiresAtUtc = null;
            user.LastActivityAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
        await auditService.WriteAsync(userId, "Logout", "User", userId.ToString(),
            "Personel oturumu güvenli biçimde kapatıldı.",
            HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return NoContent();
    }
}
