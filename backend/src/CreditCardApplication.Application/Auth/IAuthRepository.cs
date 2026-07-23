namespace CreditCardApplication.Application.Auth;

public interface IAuthRepository
{
    Task<AuthenticatedUser?> AuthenticateAsync(
        string registrationNumber,
        string password,
        string? ipAddress,
        CancellationToken cancellationToken);
}
