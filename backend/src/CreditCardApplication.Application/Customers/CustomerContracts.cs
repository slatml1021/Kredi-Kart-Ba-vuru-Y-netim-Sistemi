namespace CreditCardApplication.Application.Customers;

public sealed record CreateCustomerRequest(
    string NationalIdentityNumber,
    string FirstName,
    string LastName,
    string PhoneNumber,
    string EmailAddress,
    decimal MonthlyNetIncome,
    decimal OtherBankTotalCardLimit);

public sealed record UpdateCustomerRequest(
    string FirstName,
    string LastName,
    string PhoneNumber,
    string EmailAddress,
    decimal MonthlyNetIncome,
    decimal OtherBankTotalCardLimit);

public sealed record CustomerResponse(
    int Id,
    string CustomerNumber,
    string NationalIdentityNumber,
    string FirstName,
    string LastName,
    string PhoneNumber,
    string EmailAddress,
    decimal MonthlyNetIncome,
    decimal OtherBankTotalCardLimit,
    bool IsActive);
