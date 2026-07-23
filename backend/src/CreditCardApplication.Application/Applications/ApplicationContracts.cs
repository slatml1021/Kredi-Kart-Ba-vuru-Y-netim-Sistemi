namespace CreditCardApplication.Application.Applications;

public sealed record CreateCardApplicationRequest(
    int CustomerId,
    int CardTypeId,
    decimal RequestedLimit);

public sealed record EvaluateCardApplicationRequest(
    string Decision,
    decimal? ApprovedLimit,
    string? Note);

public sealed record ResubmitCardApplicationRequest(
    int CardTypeId,
    decimal RequestedLimit);

public sealed record CardTypeResponse(
    int Id,
    string Name,
    decimal? MinimumLimit,
    decimal? MaximumLimit);

public sealed record CardApplicationResponse(
    int Id,
    string ApplicationNumber,
    int CustomerId,
    string CustomerNumber,
    string CustomerFullName,
    int CardTypeId,
    string CardTypeName,
    decimal RequestedLimit,
    decimal AvailableLimit,
    string Status,
    DateTime CreatedAtUtc);

public sealed record ApplicationHistoryResponse(
    string? PreviousStatus,
    string NewStatus,
    string? Description,
    string ChangedBy,
    DateTime ChangedAtUtc);

public sealed record CardApplicationDetailResponse(
    CardApplicationResponse Application,
    decimal? ApprovedLimit,
    string? EvaluationNote,
    int? CreditCardId,
    IReadOnlyList<ApplicationHistoryResponse> History);
