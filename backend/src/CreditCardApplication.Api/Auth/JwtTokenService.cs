using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CreditCardApplication.Application.Auth;
using Microsoft.IdentityModel.Tokens;

namespace CreditCardApplication.Api.Auth;

public sealed class JwtTokenService(IConfiguration configuration)
{
    public LoginResponse Create(LoginResult loginResult)
    {
        var jwt = configuration.GetSection("Jwt");
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(jwt.GetValue<int>("ExpiryMinutes", 60));
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, loginResult.User.Id.ToString()),
            new(ClaimTypes.NameIdentifier, loginResult.User.Id.ToString()),
            new(ClaimTypes.Name, loginResult.User.FullName),
            new("registrationNumber", loginResult.User.RegistrationNumber)
        };
        claims.AddRange(loginResult.User.Roles.Select(x => new Claim(ClaimTypes.Role, x)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]
            ?? throw new InvalidOperationException("JWT anahtarı tanımlı değil.")));
        var token = new JwtSecurityToken(
            issuer: jwt["Issuer"], audience: jwt["Audience"], claims: claims,
            expires: expiresAtUtc, signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new LoginResponse(new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc, loginResult.User);
    }
}

public sealed record LoginResponse(string AccessToken, DateTime ExpiresAtUtc, AuthenticatedUser User);
