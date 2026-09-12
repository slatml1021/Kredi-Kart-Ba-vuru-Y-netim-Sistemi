namespace CreditCardApplication.Domain.Entities;

public sealed class ChatMessage : BaseEntity
{
    public int SenderUserId { get; set; }
    public User SenderUser { get; set; } = null!;
    public int RecipientUserId { get; set; }
    public User RecipientUser { get; set; } = null!;
    public int? CardApplicationId { get; set; }
    public CardApplication? CardApplication { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime? ReadAtUtc { get; set; }
}
