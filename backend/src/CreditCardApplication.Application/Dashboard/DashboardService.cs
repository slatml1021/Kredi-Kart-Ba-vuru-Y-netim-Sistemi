namespace CreditCardApplication.Application.Dashboard;

public sealed class DashboardService(IDashboardRepository dashboardRepository)
{
    public Task<OfficerDashboardResponse> GetOfficerAsync(int officerUserId, CancellationToken cancellationToken) =>
        dashboardRepository.GetOfficerAsync(officerUserId, cancellationToken);

    public Task<ManagerDashboardResponse> GetManagerAsync(CancellationToken cancellationToken) =>
        dashboardRepository.GetManagerAsync(cancellationToken);
}
