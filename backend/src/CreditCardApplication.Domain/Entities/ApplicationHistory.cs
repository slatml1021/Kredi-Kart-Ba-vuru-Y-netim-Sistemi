using CreditCardApplication.Domain.Enums;

namespace CreditCardApplication.Domain.Entities;

public sealed class ApplicationHistory : BaseEntity
{
    public int CardApplicationId { get; set; }
    public CardApplication CardApplication { get; set; } = null!;
    public ApplicationStatus? PreviousStatus { get; set; }
    public ApplicationStatus NewStatus { get; set; }
    public int ChangedByUserId { get; set; }
    public User ChangedByUser { get; set; } = null!;
    public string? Description { get; set; }
}
