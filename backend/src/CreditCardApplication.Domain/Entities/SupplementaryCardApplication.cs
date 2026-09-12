namespace CreditCardApplication.Domain.Entities;

public sealed class SupplementaryCardApplication : BaseEntity
{
    public string ApplicationNumber { get; set; } = string.Empty;
    public int PrimaryCustomerId { get; set; }
    public Customer PrimaryCustomer { get; set; } = null!;
    public int PrimaryCreditCardId { get; set; }
    public CreditCard PrimaryCreditCard { get; set; } = null!;
    public int SupplementaryHolderCustomerId { get; set; }
    public Customer SupplementaryHolderCustomer { get; set; } = null!;
    public string Relationship { get; set; } = string.Empty;
    public decimal RequestedLimit { get; set; }
    public string DeliveryMethod { get; set; } = "RegisteredAddress";
    public string DeliveryAddress { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public int? EvaluatedByUserId { get; set; }
    public User? EvaluatedByUser { get; set; }
    public string? EvaluationNote { get; set; }
    public DateTime? EvaluatedAtUtc { get; set; }
    public string? MaskedCardNumber { get; set; }
    public DateTime? IssuedAtUtc { get; set; }
    public string CardStatus { get; set; } = "NotCreated";
    public string? FulfillmentStatus { get; set; }
    public DateTime? EstimatedPrintAtUtc { get; set; }
    public DateTime? EstimatedDeliveryAtUtc { get; set; }
    public DateTime? NextTransitionAtUtc { get; set; }
    public DateTime? PrintingStartedAtUtc { get; set; }
    public DateTime? ShippedAtUtc { get; set; }
    public DateTime? DeliveredAtUtc { get; set; }
    public string? TrackingNumber { get; set; }
}
