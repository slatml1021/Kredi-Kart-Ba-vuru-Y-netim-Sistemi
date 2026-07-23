namespace CreditCardApplication.Application.Cards;

public sealed record CreditCardResponse(
    int Id,
    string MaskedCardNumber,
    decimal CardLimit,
    string Status,
    DateOnly IssueDate,
    DateOnly ExpiryDate,
    int ApplicationId,
    string ApplicationNumber,
    string CustomerFullName,
    string CardTypeName);
