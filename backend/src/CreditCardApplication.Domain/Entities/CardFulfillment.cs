namespace CreditCardApplication.Domain.Entities;

public sealed class CardFulfillment : BaseEntity
{
    public int CreditCardId { get; set; }
    public CreditCard CreditCard { get; set; } = null!;
    public string Status { get; set; } = "Production";
    public DateTime ProductionStartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PrintingStartedAtUtc { get; set; }
    public DateTime? PrintedAtUtc { get; set; }
    public DateTime? ShippedAtUtc { get; set; }
    public DateTime? DeliveredAtUtc { get; set; }
    public DateTime EstimatedPrintAtUtc { get; set; }
    public DateTime EstimatedDeliveryAtUtc { get; set; }
    public DateTime NextTransitionAtUtc { get; set; }
    public string? TrackingNumber { get; set; }
}
