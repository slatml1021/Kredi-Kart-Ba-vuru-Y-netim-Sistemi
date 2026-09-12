namespace CreditCardApplication.Domain.Entities;

public sealed class CustomerConsent : BaseEntity
{
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public string ConsentType { get; set; } = string.Empty;
    public string TextVersion { get; set; } = "v2.1";
    public bool IsGranted { get; set; }
    public string Channel { get; set; } = "Şube";
    public int CapturedByUserId { get; set; }
    public User CapturedByUser { get; set; } = null!;
    public DateTime CapturedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? WithdrawnAtUtc { get; set; }
}
