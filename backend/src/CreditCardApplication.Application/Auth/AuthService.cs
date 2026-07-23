using System.Security.Authentication;

namespace CreditCardApplication.Application.Auth;

public sealed class AuthService(IAuthRepository authRepository)
{
    public async Task<LoginResult> LoginAsync(
        LoginRequest request,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RegistrationNumber) || string.IsNullOrWhiteSpace(request.Password))
            throw new ArgumentException("Sicil numarası ve parola zorunludur.");

        var user = await authRepository.AuthenticateAsync(
            request.RegistrationNumber.Trim(), request.Password, ipAddress, cancellationToken);
        if (user is null)
            throw new AuthenticationException("Sicil numarası veya parola hatalı.");

        return new LoginResult(user);
    }
}
