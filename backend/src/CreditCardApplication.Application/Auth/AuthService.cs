using System.Security.Authentication;

namespace CreditCardApplication.Application.Auth;

public sealed class AuthService(IAuthRepository authRepository)
{
    private static readonly System.Text.RegularExpressions.Regex RegistrationNumberPattern =
        new("^KBP[0-9]{6}$", System.Text.RegularExpressions.RegexOptions.Compiled);
    private static readonly System.Text.RegularExpressions.Regex PasswordPattern =
        new("^(?=.{12,64}$)(?=.*[a-zçğıöşü])(?=.*[A-ZÇĞİÖŞÜ])(?=.*\\d)(?=.*[^A-Za-zÇĞİÖŞÜçğıöşü0-9]).+$",
            System.Text.RegularExpressions.RegexOptions.Compiled);

    public async Task<LoginResult> LoginAsync(
        LoginRequest request,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RegistrationNumber) || string.IsNullOrWhiteSpace(request.Password))
            throw new ArgumentException("Sicil numarası ve parola zorunludur.");

        var registrationNumber = request.RegistrationNumber.Trim().ToUpperInvariant();
        if (!RegistrationNumberPattern.IsMatch(registrationNumber))
            throw new AuthenticationException("Sicil numarası veya parola hatalı.");
        if (!PasswordPattern.IsMatch(request.Password))
            throw new AuthenticationException("Sicil numarası veya parola hatalı.");

        var attempt = await authRepository.AuthenticateAsync(
            registrationNumber, request.Password, ipAddress, cancellationToken);
        if (attempt.IsLocked)
            throw new AccountLockedException();
        if (attempt.User is null)
            throw new AuthenticationException("Sicil numarası veya parola hatalı.");

        return new LoginResult(attempt.User);
    }

    public Task ReportLoginIssueAsync(
        ReportLoginIssueRequest request,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var registrationNumber = request.RegistrationNumber.Trim().ToUpperInvariant();
        if (!RegistrationNumberPattern.IsMatch(registrationNumber))
            throw new ArgumentException("Sorun bildirimi için geçerli bir sicil numarası girilmelidir.");
        return authRepository.ReportLoginIssueAsync(registrationNumber, ipAddress, cancellationToken);
    }
}
