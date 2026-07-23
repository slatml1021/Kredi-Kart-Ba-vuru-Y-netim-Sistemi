namespace CreditCardApplication.Domain.Entities;

public sealed class User : BaseEntity
{
    public string RegistrationNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int FailedLoginCount { get; set; }
    public DateTime? LockoutEndUtc { get; set; }
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
