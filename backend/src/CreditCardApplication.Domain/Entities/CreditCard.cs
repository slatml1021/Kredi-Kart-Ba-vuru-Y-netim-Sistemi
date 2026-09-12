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
    public decimal? RequestedNewLimit { get; set; }
    public string? LimitIncreaseStatus { get; set; }
    public string? LimitChangeType { get; set; }
    public DateTime? LimitIncreaseRequestedAtUtc { get; set; }
    public string? LimitIncreaseEvaluationNote { get; set; }
    public DateTime? LimitIncreaseEvaluatedAtUtc { get; set; }
    public int? LimitIncreaseEvaluatedByUserId { get; set; }
    public CardFulfillment? Fulfillment { get; set; }
}
