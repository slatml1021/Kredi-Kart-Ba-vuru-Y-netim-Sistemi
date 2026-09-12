namespace CreditCardApplication.Application.Dashboard;

public sealed class DashboardService(IDashboardRepository dashboardRepository)
{
    public Task<OfficerDashboardResponse> GetOfficerAsync(int officerUserId, CancellationToken cancellationToken) =>
        dashboardRepository.GetOfficerAsync(officerUserId, cancellationToken);

    public Task<ManagerDashboardResponse> GetManagerAsync(
        string period, int? month, int? year, CancellationToken cancellationToken) =>
        dashboardRepository.GetManagerAsync(period, month, year, cancellationToken);
}
