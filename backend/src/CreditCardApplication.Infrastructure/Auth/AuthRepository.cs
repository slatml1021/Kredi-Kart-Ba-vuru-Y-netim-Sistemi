using CreditCardApplication.Application.Auth;
using CreditCardApplication.Domain.Entities;
using CreditCardApplication.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CreditCardApplication.Infrastructure.Auth;

public sealed class AuthRepository(ApplicationDbContext dbContext, IConfiguration configuration) : IAuthRepository
{
    private const int MaximumFailedAttempts = 3;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(10);

    public async Task<AuthenticationAttempt> AuthenticateAsync(
        string registrationNumber,
        string password,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(x => x.RegistrationNumber == registrationNumber, cancellationToken);

        if (user is null || !user.IsActive)
        {
            await AddLoginHistoryAsync(user?.Id, registrationNumber, false, "Geçersiz veya pasif kullanıcı.", ipAddress, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return new(null, false);
        }

        if (user.LockoutEndUtc.HasValue && user.LockoutEndUtc.Value > DateTime.UtcNow)
        {
            await AddLoginHistoryAsync(user.Id, registrationNumber, false, "Hesap geçici olarak kilitli.", ipAddress, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return new(null, true);
        }

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            user.FailedLoginCount++;
            var isLocked = user.FailedLoginCount >= MaximumFailedAttempts;
            if (isLocked)
            {
                user.FailedLoginCount = 0;
                user.LockoutEndUtc = DateTime.UtcNow.Add(LockoutDuration);
            }

            await AddLoginHistoryAsync(user.Id, registrationNumber, false, "Parola hatalı.", ipAddress, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return new(null, isLocked);
        }

        user.FailedLoginCount = 0;
        user.LockoutEndUtc = null;
        var sessionId = Guid.NewGuid().ToString("N");
        user.ActiveSessionId = sessionId;
        user.LastActivityAtUtc = DateTime.UtcNow;
        var configuredLifetime = int.TryParse(configuration["Jwt:ExpiryMinutes"], out var minutes) ? minutes : 60;
        var sessionLifetimeMinutes = Math.Clamp(configuredLifetime, 5, 720);
        user.ActiveSessionExpiresAtUtc = DateTime.UtcNow.AddMinutes(sessionLifetimeMinutes);
        await AddLoginHistoryAsync(user.Id, registrationNumber, true, null, ipAddress, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new(new AuthenticatedUser(user.Id, user.RegistrationNumber, user.FullName,
            user.UserRoles.Select(x => x.Role.Name).ToList(), sessionId), false);
    }

    private async Task AddLoginHistoryAsync(int? userId, string registrationNumber, bool isSuccessful,
        string? failureReason, string? ipAddress, CancellationToken cancellationToken) =>
        await dbContext.LoginHistories.AddAsync(new LoginHistory
        {
            UserId = userId,
            RegistrationNumber = registrationNumber,
            IsSuccessful = isSuccessful,
            FailureReason = failureReason,
            IpAddress = ipAddress
        }, cancellationToken);

    public async Task ReportLoginIssueAsync(
        string registrationNumber,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var reportingUser = await dbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.RegistrationNumber == registrationNumber, cancellationToken);
        var managers = await dbContext.Users
            .Where(x => x.IsActive && x.NotifySecurityEvents
                && x.UserRoles.Any(role => role.Role.Name == "Manager"))
            .ToListAsync(cancellationToken);
        var message = reportingUser is null
            ? $"{registrationNumber} sicil numarası için giriş sorunu bildirildi. Kayıtlı kullanıcı bulunamadı."
            : $"{reportingUser.FullName} ({registrationNumber}) giriş yapamadığını bildirdi.";
        foreach (var manager in managers)
        {
            dbContext.Notifications.Add(new Notification
            {
                UserId = manager.Id,
                Type = "Security",
                Title = "Personel giriş sorunu",
                Message = message,
                Link = "/manager/profile"
            });
        }
        await AddLoginHistoryAsync(reportingUser?.Id, registrationNumber, false,
            "Kullanıcı giriş sorunu bildirdi.", ipAddress, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (configuration["Email:Mode"]?.Equals("File", StringComparison.OrdinalIgnoreCase) == true)
        {
            var outbox = Path.Combine(AppContext.BaseDirectory, "App_Data", "email-outbox");
            Directory.CreateDirectory(outbox);
            var file = Path.Combine(outbox, $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-login-support.txt");
            await File.WriteAllTextAsync(file,
                $"To: support@kartbasvuru.local{Environment.NewLine}Subject: Personel giriş sorunu{Environment.NewLine}{message}{Environment.NewLine}IP: {ipAddress ?? "Bilinmiyor"}",
                cancellationToken);
        }
    }
}
