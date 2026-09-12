using System.Security.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace CreditCardApplication.Api.Middleware;

public sealed class ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ArgumentException exception)
        {
            await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "Doğrulama hatası", exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            await WriteProblemAsync(context, StatusCodes.Status409Conflict, "İş kuralı ihlali", exception.Message);
        }
        catch (CreditCardApplication.Application.Auth.AccountLockedException exception)
        {
            context.Response.Headers.RetryAfter = "600";
            await WriteProblemAsync(context, StatusCodes.Status423Locked, "Hesap geçici olarak kilitlendi", exception.Message);
        }
        catch (AuthenticationException exception)
        {
            await WriteProblemAsync(context, StatusCodes.Status401Unauthorized, "Kimlik doğrulama başarısız", exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            await WriteProblemAsync(context, StatusCodes.Status403Forbidden, "Yetkisiz işlem", exception.Message);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Beklenmeyen API hatası oluştu.");
            await WriteProblemAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "Sistem hatası",
                "İşlem sırasında beklenmeyen bir hata oluştu.");
        }
    }

    private static Task WriteProblemAsync(HttpContext context, int status, string title, string detail)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        return context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        });
    }
}
