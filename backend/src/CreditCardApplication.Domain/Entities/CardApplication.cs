using CreditCardApplication.Domain.Enums;

namespace CreditCardApplication.Domain.Entities;

public sealed class CardApplication : BaseEntity
{
    public string ApplicationNumber { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public int CardTypeId { get; set; }
    public CardType CardType { get; set; } = null!;
    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public int? EvaluatedByUserId { get; set; }
    public User? EvaluatedByUser { get; set; }
    public decimal RequestedLimit { get; set; }
    public decimal? ApprovedLimit { get; set; }
    public ApplicationStatus Status { get; set; } = ApplicationStatus.Pending;
    public string? EvaluationNote { get; set; }
    public DateTime? EvaluatedAtUtc { get; set; }
    public ICollection<ApplicationHistory> Histories { get; set; } = new List<ApplicationHistory>();
    public CreditCard? CreditCard { get; set; }
}
