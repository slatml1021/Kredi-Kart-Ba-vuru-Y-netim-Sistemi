namespace CreditCardApplication.Application.Dashboard;

public interface IDashboardRepository
{
    Task<OfficerDashboardResponse> GetOfficerAsync(int officerUserId, CancellationToken cancellationToken);
    Task<ManagerDashboardResponse> GetManagerAsync(CancellationToken cancellationToken);
}
