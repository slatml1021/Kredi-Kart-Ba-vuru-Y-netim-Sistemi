using CreditCardApplication.Domain.Enums;

namespace CreditCardApplication.Domain.Entities;

public sealed class CreditCard : BaseEntity
{
    public int CardApplicationId { get; set; }
    public CardApplication CardApplication { get; set; } = null!;
    public string MaskedCardNumber { get; set; } = string.Empty;
    public decimal CardLimit { get; set; }
    public CardStatus Status { get; set; } = CardStatus.Inactive;
    public DateOnly IssueDate { get; set; }
    public DateOnly ExpiryDate { get; set; }
}
