namespace CreditCardApplication.Application.Auth;

public interface IAuthRepository
{
    Task<AuthenticationAttempt> AuthenticateAsync(
        string registrationNumber,
        string password,
        string? ipAddress,
        CancellationToken cancellationToken);
    Task ReportLoginIssueAsync(string registrationNumber, string? ipAddress, CancellationToken cancellationToken);
}
