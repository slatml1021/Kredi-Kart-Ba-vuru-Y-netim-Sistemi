using CreditCardApplication.Application.Auth;
using CreditCardApplication.Domain.Entities;
using CreditCardApplication.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreditCardApplication.Infrastructure.Auth;

public sealed class AuthRepository(ApplicationDbContext dbContext) : IAuthRepository
{
    private const int MaximumFailedAttempts = 3;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(10);

    public async Task<AuthenticatedUser?> AuthenticateAsync(
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
            return null;
        }

        if (user.LockoutEndUtc.HasValue && user.LockoutEndUtc.Value > DateTime.UtcNow)
        {
            await AddLoginHistoryAsync(user.Id, registrationNumber, false, "Hesap geçici olarak kilitli.", ipAddress, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= MaximumFailedAttempts)
            {
                user.FailedLoginCount = 0;
                user.LockoutEndUtc = DateTime.UtcNow.Add(LockoutDuration);
            }

            await AddLoginHistoryAsync(user.Id, registrationNumber, false, "Parola hatalı.", ipAddress, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }

        user.FailedLoginCount = 0;
        user.LockoutEndUtc = null;
        await AddLoginHistoryAsync(user.Id, registrationNumber, true, null, ipAddress, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new AuthenticatedUser(user.Id, user.RegistrationNumber, user.FullName,
            user.UserRoles.Select(x => x.Role.Name).ToList());
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
}
