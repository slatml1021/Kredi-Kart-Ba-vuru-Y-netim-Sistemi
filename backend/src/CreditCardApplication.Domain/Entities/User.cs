namespace CreditCardApplication.Domain.Entities;

public sealed class User : BaseEntity
{
    public string RegistrationNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int FailedLoginCount { get; set; }
    public DateTime? LockoutEndUtc { get; set; }
    public string CorporateEmail { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public string? ProfilePhotoUrl { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public DateTime? PasswordChangedAtUtc { get; set; }
    public bool NotifyApplicationEvents { get; set; } = true;
    public bool NotifySlaWarnings { get; set; } = true;
    public bool NotifySecurityEvents { get; set; } = true;
    public string? ActiveSessionId { get; set; }
    public DateTime? ActiveSessionExpiresAtUtc { get; set; }
    public DateTime? LastActivityAtUtc { get; set; }
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
