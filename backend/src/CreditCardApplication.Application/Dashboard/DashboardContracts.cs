namespace CreditCardApplication.Application.Dashboard;

public sealed record OfficerDashboardResponse(
    int TotalApplications,
    int PendingApplications,
    int RevisionApplications,
    int ApprovedApplications,
    int RejectedApplications,
    IReadOnlyList<RecentOfficerRequestResponse> RecentRequests);

public sealed record RecentOfficerRequestResponse(
    string RequestType,
    int EntityId,
    string ReferenceNumber,
    int CustomerId,
    string CustomerName,
    string Description,
    string Status,
    DateTime CreatedAtUtc);

public sealed record ManagerDashboardResponse(
    int PendingApplications,
    int ApprovedToday,
    int RejectedToday,
    int RevisionApplications,
    int ProcessedApplications,
    int ApprovedApplications,
    int RejectedApplications,
    decimal ApprovalRate,
    decimal RejectionRate,
    int TotalApplications,
    IReadOnlyList<CardTypeDistributionResponse> CardTypeDistribution,
    IReadOnlyList<ApplicationTrendResponse> ApplicationTrend,
    IReadOnlyList<OfficerPerformanceResponse> OfficerPerformance);

public sealed record CardTypeDistributionResponse(string CardType, int Count, decimal Percentage);
public sealed record ApplicationTrendResponse(string Label, int Count);
public sealed record OfficerPerformanceResponse(
    string OfficerName,
    int CreatedApplications,
    decimal ApprovalRate,
    int AverageProcessingMinutes);
