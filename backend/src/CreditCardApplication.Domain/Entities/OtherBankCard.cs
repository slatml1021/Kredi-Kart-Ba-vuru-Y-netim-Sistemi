namespace CreditCardApplication.Domain.Entities;

public sealed class OtherBankCard : BaseEntity
{
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public string BankName { get; set; } = string.Empty;
    public string? MaskedCardNumber { get; set; }
    public decimal CardLimit { get; set; }
    public bool IsActive { get; set; } = true;
}
