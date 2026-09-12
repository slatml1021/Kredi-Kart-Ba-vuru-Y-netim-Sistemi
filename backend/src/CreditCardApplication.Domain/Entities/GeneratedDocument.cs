namespace CreditCardApplication.Domain.Entities;

public sealed class GeneratedDocument : BaseEntity
{
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public string EmailStatus { get; set; } = "NotSent";
    public DateTime? EmailedAtUtc { get; set; }
    public string? EmailFailureReason { get; set; }
    public string VerificationStatus { get; set; } = "PendingVerification";
    public int? VerifiedByUserId { get; set; }
    public User? VerifiedByUser { get; set; }
    public DateTime? VerifiedAtUtc { get; set; }
    public string? VerificationNote { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
}
