namespace CreditCardApplication.Application.Auth;

public sealed record LoginRequest(string RegistrationNumber, string Password);

public sealed record AuthenticatedUser(int Id, string RegistrationNumber, string FullName, IReadOnlyList<string> Roles);

public sealed record LoginResult(AuthenticatedUser User);
