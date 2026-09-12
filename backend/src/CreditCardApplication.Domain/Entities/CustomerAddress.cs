namespace CreditCardApplication.Domain.Entities;

public sealed class CustomerAddress : BaseEntity
{
    public int CustomerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string Neighborhood { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string? Avenue { get; set; }
    public string BuildingNo { get; set; } = string.Empty;
    public string? ApartmentNo { get; set; }
    public string? Floor { get; set; }
    public string PostalCode { get; set; } = string.Empty;
    public string FullAddress { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public Customer Customer { get; set; } = null!;
}
