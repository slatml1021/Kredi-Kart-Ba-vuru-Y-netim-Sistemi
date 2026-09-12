using System.Security.Claims;
using CreditCardApplication.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace CreditCardApplication.Api.Middleware;

public sealed class SessionValidationMiddleware(RequestDelegate next, IConfiguration configuration)
{
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(20);
    private readonly int sessionLifetimeMinutes = Math.Clamp(
        int.TryParse(configuration["Jwt:ExpiryMinutes"], out var minutes) ? minutes : 60, 5, 720);

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && (context.User.IsInRole("Officer") || context.User.IsInRole("Manager")))
        {
            var userIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var sessionId = context.User.FindFirstValue("sid");
            if (!int.TryParse(userIdValue, out var userId) || string.IsNullOrWhiteSpace(sessionId))
            {
                await RejectAsync(context, "Oturum kimliği geçersiz.");
                return;
            }
            var user = await db.Users.FirstOrDefaultAsync(x => x.Id == userId, context.RequestAborted);
            var now = DateTime.UtcNow;
            var invalid = user is null || !user.IsActive || user.ActiveSessionId != sessionId
                || user.ActiveSessionExpiresAtUtc <= now
                || (user.LastActivityAtUtc.HasValue && now - user.LastActivityAtUtc.Value > IdleTimeout);
            if (invalid)
            {
                if (user is not null && user.ActiveSessionId == sessionId)
                {
                    user.ActiveSessionId = null;
                    user.ActiveSessionExpiresAtUtc = null;
                    await db.SaveChangesAsync(context.RequestAborted);
                }
                await RejectAsync(context, "Oturum sona erdi veya başka bir cihazdan yeni oturum açıldı.");
                return;
            }
            if (!user!.LastActivityAtUtc.HasValue || now - user.LastActivityAtUtc.Value > TimeSpan.FromMinutes(1))
            {
                user.LastActivityAtUtc = now;
                user.ActiveSessionExpiresAtUtc = now.AddMinutes(sessionLifetimeMinutes);
                await db.SaveChangesAsync(context.RequestAborted);
            }
        }
        await next(context);
    }

    private static async Task RejectAsync(HttpContext context, string detail)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Oturum doğrulanamadı",
            Detail = detail
        });
    }
}
