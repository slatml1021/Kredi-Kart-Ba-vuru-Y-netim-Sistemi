namespace CreditCardApplication.Application.Customers;

public sealed record CreateCustomerRequest(
    string NationalIdentityNumber,
    string FirstName,
    string LastName,
    string PhoneCountryCode,
    string PhoneNumber,
    string EmailAddress,
    decimal MonthlyNetIncome,
    decimal OtherBankTotalCardLimit,
    DateOnly? BirthDate = null,
    string Gender = "Belirtilmedi",
    string EducationLevel = "Belirtilmedi",
    string Occupation = "Belirtilmedi",
    string EmploymentStatus = "Çalışıyor",
    string City = "",
    string District = "",
    string Neighborhood = "",
    string Address = "",
    IReadOnlyList<OtherBankCardRequest>? OtherBankCards = null,
    bool KvkkConsentGranted = true,
    bool SmsConsentGranted = false,
    bool EmailConsentGranted = false,
    string Street = "",
    string? Avenue = null,
    string BuildingNo = "",
    string? ApartmentNo = null,
    string? Floor = null,
    string PostalCode = "");

public sealed record UpdateCustomerRequest(
    string FirstName,
    string LastName,
    string PhoneCountryCode,
    string PhoneNumber,
    string EmailAddress,
    decimal MonthlyNetIncome,
    decimal OtherBankTotalCardLimit,
    DateOnly? BirthDate = null,
    string Gender = "Belirtilmedi",
    string EducationLevel = "Belirtilmedi",
    string Occupation = "Belirtilmedi",
    string EmploymentStatus = "Çalışıyor",
    string City = "",
    string District = "",
    string Neighborhood = "",
    string Address = "",
    IReadOnlyList<OtherBankCardRequest>? OtherBankCards = null);

public sealed record OtherBankCardRequest(string BankName, string? MaskedCardNumber, decimal CardLimit);
public sealed record OtherBankCardResponse(int Id, string BankName, string? MaskedCardNumber, decimal CardLimit, bool IsActive);
public sealed record ExternalRiskCardResponse(string BankName, string CardLastFourDigits, decimal CardLimit);
public sealed record ExternalRiskProfileResponse(
    decimal OtherBankTotalCardLimit,
    IReadOnlyList<ExternalRiskCardResponse> Cards);
public sealed record RequestCustomerContactVerificationRequest(string Channel);
public sealed record CustomerContactVerificationResponse(
    string Channel,
    string MaskedDestination,
    DateTime ExpiresAtUtc,
    string DemoCode);
public sealed record VerifyCustomerContactRequest(string Channel, string Code);
public sealed record SetCustomerStatusRequest(bool IsActive);
public sealed record CustomerAddressRequest(
    string Name,
    string City,
    string District,
    string Neighborhood,
    string Street,
    string? Avenue,
    string BuildingNo,
    string? ApartmentNo,
    string? Floor,
    string PostalCode,
    bool IsDefault = false);
public sealed record CustomerAddressResponse(
    int Id,
    int CustomerId,
    string Name,
    string City,
    string District,
    string Neighborhood,
    string Street,
    string? Avenue,
    string BuildingNo,
    string? ApartmentNo,
    string? Floor,
    string PostalCode,
    string FullAddress,
    bool IsDefault,
    bool IsActive);

public interface IContactVerificationSender
{
    Task SendAsync(string channel, string destination, string code, CancellationToken cancellationToken);
    bool ExposeDemoCode { get; }
}

public sealed record CustomerResponse(
    int Id,
    string CustomerNumber,
    string NationalIdentityNumber,
    string FirstName,
    string LastName,
    string PhoneCountryCode,
    string PhoneNumber,
    bool IsPhoneVerified,
    string EmailAddress,
    bool IsEmailVerified,
    DateOnly? BirthDate,
    string Gender,
    string EducationLevel,
    string Occupation,
    string EmploymentStatus,
    string City,
    string District,
    string Neighborhood,
    string Address,
    decimal MonthlyNetIncome,
    decimal OtherBankTotalCardLimit,
    int CreditScore,
    bool IsActive,
    bool IsProfileComplete,
    IReadOnlyList<string> MissingProfileFields,
    decimal OwnBankTotalCardLimit,
    decimal AvailableCardLimit,
    IReadOnlyList<OtherBankCardResponse> OtherBankCards);

public sealed record CustomerCardSummary(
    int Id,
    string MaskedCardNumber,
    string CardTypeName,
    decimal CardLimit,
    string Status);

public sealed record CustomerApplicationSummary(
    int Id,
    string ApplicationNumber,
    string CardTypeName,
    decimal RequestedLimit,
    string Status,
    DateTime CreatedAtUtc);

public sealed record CustomerDetailResponse(
    CustomerResponse Customer,
    IReadOnlyList<CustomerCardSummary> Cards,
    IReadOnlyList<CustomerApplicationSummary> Applications);
