namespace CreditCardApplication.Application.Dashboard;

public interface IDashboardRepository
{
    Task<OfficerDashboardResponse> GetOfficerAsync(int officerUserId, CancellationToken cancellationToken);
    Task<ManagerDashboardResponse> GetManagerAsync(
        string period, int? month, int? year, CancellationToken cancellationToken);
}
