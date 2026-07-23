using CreditCardApplication.Application.Dashboard;
using CreditCardApplication.Domain.Enums;
using CreditCardApplication.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreditCardApplication.Infrastructure.Dashboard;

public sealed class DashboardRepository(ApplicationDbContext dbContext) : IDashboardRepository
{
    public async Task<OfficerDashboardResponse> GetOfficerAsync(int officerUserId, CancellationToken cancellationToken)
    {
        var applications = dbContext.CardApplications.Where(x => x.CreatedByUserId == officerUserId);
        return new OfficerDashboardResponse(
            await applications.CountAsync(cancellationToken),
            await applications.CountAsync(x => x.Status == ApplicationStatus.Pending, cancellationToken),
            await applications.CountAsync(x => x.Status == ApplicationStatus.Revision, cancellationToken),
            await applications.CountAsync(x => x.Status == ApplicationStatus.Approved, cancellationToken));
    }

    public async Task<ManagerDashboardResponse> GetManagerAsync(CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        return new ManagerDashboardResponse(
            await dbContext.CardApplications.CountAsync(x => x.Status == ApplicationStatus.Pending, cancellationToken),
            await dbContext.CardApplications.CountAsync(x => x.Status == ApplicationStatus.Approved && x.EvaluatedAtUtc >= today, cancellationToken),
            await dbContext.CardApplications.CountAsync(x => x.Status == ApplicationStatus.Rejected && x.EvaluatedAtUtc >= today, cancellationToken),
            await dbContext.CardApplications.CountAsync(x => x.Status == ApplicationStatus.Revision, cancellationToken));
    }
}
