namespace CreditCardApplication.Application.Cards;

public sealed record CreateLimitIncreaseRequest(decimal RequestedNewLimit);
public sealed record CreateLimitChangeRequest(string ChangeType, decimal RequestedNewLimit);
public sealed record EvaluateLimitIncreaseRequest(string Decision, string? Note);

public sealed record LimitIncreaseRequestResponse(
    int CardId,
    string MaskedCardNumber,
    string CustomerNumber,
    string CustomerFullName,
    string CardTypeName,
    decimal CurrentLimit,
    decimal RequestedNewLimit,
    string ChangeType,
    string Status,
    DateTime RequestedAtUtc,
    string? EvaluationNote,
    DateTime? EvaluatedAtUtc);

public sealed record CreditCardResponse(
    int Id,
    string MaskedCardNumber,
    decimal CardLimit,
    string Status,
    DateOnly IssueDate,
    DateOnly ExpiryDate,
    int ApplicationId,
    string ApplicationNumber,
    string CustomerNumber,
    string CustomerFullName,
    string CardTypeName,
    string DeliveryMethod,
    string DeliveryAddress,
    string StatementPreference,
    bool ContactlessEnabled,
    bool InternetShoppingEnabled,
    decimal CardTypeMaximumLimit,
    decimal CustomerMaximumLimit,
    decimal? RequestedNewLimit,
    string? LimitIncreaseStatus,
    string? LimitChangeType,
    DateTime? LimitIncreaseRequestedAtUtc,
    string? LimitIncreaseEvaluationNote,
    DateTime? LimitIncreaseEvaluatedAtUtc);
