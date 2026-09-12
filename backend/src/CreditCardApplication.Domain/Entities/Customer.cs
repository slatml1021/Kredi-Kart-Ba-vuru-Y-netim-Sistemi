namespace CreditCardApplication.Domain.Entities;

public sealed class Customer : BaseEntity
{
    public string CustomerNumber { get; set; } = string.Empty;
    public string NationalIdentityNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PhoneCountryCode { get; set; } = "+90";
    public string PhoneNumber { get; set; } = string.Empty;
    public bool IsPhoneVerified { get; set; }
    public string? PhoneVerificationCodeHash { get; set; }
    public DateTime? PhoneVerificationExpiresAtUtc { get; set; }
    public int PhoneVerificationFailedAttempts { get; set; }
    public string EmailAddress { get; set; } = string.Empty;
    public bool IsEmailVerified { get; set; }
    public string? EmailVerificationCodeHash { get; set; }
    public DateTime? EmailVerificationExpiresAtUtc { get; set; }
    public int EmailVerificationFailedAttempts { get; set; }
    public DateOnly? BirthDate { get; set; }
    public string Gender { get; set; } = "Belirtilmedi";
    public string EducationLevel { get; set; } = "Belirtilmedi";
    public string Occupation { get; set; } = "Belirtilmedi";
    public string City { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string Neighborhood { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public decimal MonthlyNetIncome { get; set; }
    public decimal OtherBankTotalCardLimit { get; set; }
    public int CreditScore { get; set; }
    public bool IsActive { get; set; } = true;
    public string EmploymentStatus { get; set; } = "Aktif";
    public ICollection<CardApplication> Applications { get; set; } = new List<CardApplication>();
    public ICollection<OtherBankCard> OtherBankCards { get; set; } = new List<OtherBankCard>();
    public ICollection<CustomerConsent> Consents { get; set; } = new List<CustomerConsent>();
    public ICollection<CustomerAddress> SavedAddresses { get; set; } = new List<CustomerAddress>();
}
