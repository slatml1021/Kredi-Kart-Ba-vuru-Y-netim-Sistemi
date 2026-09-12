namespace CreditCardApplication.Application.Auth;

public sealed record LoginRequest(string RegistrationNumber, string Password);
public sealed record ReportLoginIssueRequest(string RegistrationNumber);

public sealed record AuthenticatedUser(int Id, string RegistrationNumber, string FullName,
    IReadOnlyList<string> Roles, string SessionId);
public sealed record AuthenticationAttempt(AuthenticatedUser? User, bool IsLocked);

public sealed record LoginResult(AuthenticatedUser User);

public sealed class AccountLockedException : System.Security.Authentication.AuthenticationException
{
    public AccountLockedException() : base("Hesap geçici olarak kilitlendi.") { }
}
