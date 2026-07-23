namespace CreditCardApplication.Application.Dashboard;

public sealed record OfficerDashboardResponse(
    int TotalApplications,
    int PendingApplications,
    int RevisionApplications,
    int ApprovedApplications,
    int RejectedApplications);

public sealed record ManagerDashboardResponse(
    int PendingApplications,
    int ApprovedToday,
    int RejectedToday,
    int RevisionApplications);
