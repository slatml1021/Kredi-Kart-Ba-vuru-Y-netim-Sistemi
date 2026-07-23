namespace CreditCardApplication.Domain.Entities;

public sealed class LoginHistory : BaseEntity
{
    public int? UserId { get; set; }
    public User? User { get; set; }
    public string RegistrationNumber { get; set; } = string.Empty;
    public bool IsSuccessful { get; set; }
    public string? IpAddress { get; set; }
    public string? FailureReason { get; set; }
}
