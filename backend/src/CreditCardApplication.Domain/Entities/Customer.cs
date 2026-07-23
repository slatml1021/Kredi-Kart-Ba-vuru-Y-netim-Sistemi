namespace CreditCardApplication.Domain.Entities;

public sealed class Customer : BaseEntity
{
    public string CustomerNumber { get; set; } = string.Empty;
    public string NationalIdentityNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string EmailAddress { get; set; } = string.Empty;
    public decimal MonthlyNetIncome { get; set; }
    public decimal OtherBankTotalCardLimit { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<CardApplication> Applications { get; set; } = new List<CardApplication>();
}
