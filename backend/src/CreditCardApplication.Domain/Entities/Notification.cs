namespace CreditCardApplication.Domain.Entities;

public sealed class Notification : BaseEntity
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string Type { get; set; } = "Info";
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Link { get; set; }
    public bool IsRead { get; set; }
}
