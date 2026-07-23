namespace CreditCardApplication.Domain.Entities;

public sealed class CardType : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Bin { get; set; } = string.Empty;
    public decimal? MinimumLimit { get; set; }
    public decimal? MaximumLimit { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<CardApplication> Applications { get; set; } = new List<CardApplication>();
}
