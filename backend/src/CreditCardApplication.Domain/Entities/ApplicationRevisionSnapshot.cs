namespace CreditCardApplication.Domain.Entities;

public sealed class ApplicationRevisionSnapshot : BaseEntity
{
    public int CardApplicationId { get; set; }
    public CardApplication CardApplication { get; set; } = null!;
    public int RevisionNumber { get; set; }
    public string Stage { get; set; } = "Before";
    public string DataJson { get; set; } = "{}";
    public int CapturedByUserId { get; set; }
    public User CapturedByUser { get; set; } = null!;
}
