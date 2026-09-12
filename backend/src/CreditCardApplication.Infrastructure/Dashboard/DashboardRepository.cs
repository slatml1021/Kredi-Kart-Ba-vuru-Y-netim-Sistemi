using CreditCardApplication.Application.Dashboard;
using CreditCardApplication.Domain.Enums;
using CreditCardApplication.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreditCardApplication.Infrastructure.Dashboard;

public sealed class DashboardRepository(ApplicationDbContext dbContext) : IDashboardRepository
{
    public async Task<OfficerDashboardResponse> GetOfficerAsync(int officerUserId, CancellationToken cancellationToken)
    {
        var applications = await dbContext.CardApplications.AsNoTracking()
            .Where(x => x.CreatedByUserId == officerUserId)
            .Include(x => x.Customer).Include(x => x.CardType)
            .ToListAsync(cancellationToken);
        var cardRequests = applications.Select(x => new RecentOfficerRequestResponse(
                "CardApplication", x.Id, x.ApplicationNumber, x.CustomerId,
                x.Customer.FirstName + " " + x.Customer.LastName,
                x.CardType.Name + " kart başvurusu", x.Status.ToString(), x.CreatedAtUtc))
            .ToList();
        var supplementaryEntities = await dbContext.SupplementaryCardApplications.AsNoTracking()
            .Where(x => x.CreatedByUserId == officerUserId)
            .Include(x => x.PrimaryCustomer).Include(x => x.SupplementaryHolderCustomer)
            .ToListAsync(cancellationToken);
        var supplementaryRequests = supplementaryEntities.Select(x => new RecentOfficerRequestResponse(
                "SupplementaryCard", x.Id, x.ApplicationNumber, x.PrimaryCustomerId,
                x.PrimaryCustomer.FirstName + " " + x.PrimaryCustomer.LastName,
                x.SupplementaryHolderCustomer.FirstName + " " + x.SupplementaryHolderCustomer.LastName + " adına ek kart",
                x.Status, x.CreatedAtUtc))
            .ToList();
        var limitEntities = await dbContext.CreditCards.AsNoTracking()
            .Where(x => x.CardApplication.CreatedByUserId == officerUserId
                        && x.LimitIncreaseRequestedAtUtc.HasValue && x.LimitChangeType != null)
            .Include(x => x.CardApplication).ThenInclude(x => x.Customer)
            .ToListAsync(cancellationToken);
        var limitRequests = limitEntities.Select(x => new RecentOfficerRequestResponse(
                x.LimitChangeType == "Decrease" ? "LimitDecrease" : "LimitIncrease",
                x.Id,
                "LMT-" + x.Id,
                x.CardApplication.CustomerId,
                x.CardApplication.Customer.FirstName + " " + x.CardApplication.Customer.LastName,
                (x.LimitChangeType == "Decrease" ? "Limit azaltımı" : "Limit artırımı") + " · "
                    + (x.RequestedNewLimit.HasValue ? x.RequestedNewLimit.Value.ToString("N0") + " TL" : "—"),
                x.LimitIncreaseStatus ?? "Pending",
                x.LimitIncreaseRequestedAtUtc!.Value))
            .ToList();
        var recentRequests = cardRequests.Concat(supplementaryRequests).Concat(limitRequests)
            .OrderByDescending(x => x.CreatedAtUtc).Take(10).ToList();

        return new OfficerDashboardResponse(
            applications.Count,
            applications.Count(x => x.Status == ApplicationStatus.Pending),
            applications.Count(x => x.Status == ApplicationStatus.Revision),
            applications.Count(x => x.Status == ApplicationStatus.Approved),
            applications.Count(x => x.Status == ApplicationStatus.Rejected),
            recentRequests);
    }

    public async Task<ManagerDashboardResponse> GetManagerAsync(
        string period, int? month, int? year, CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        var normalizedPeriod = period.Trim().ToLowerInvariant();
        var selectedYear = year ?? today.Year;
        if (selectedYear is < 2020 or > 2100)
            throw new ArgumentException("Analiz yılı 2020 ile 2100 arasında olmalıdır.");
        var selectedMonth = month ?? today.Month;
        if (selectedMonth is < 1 or > 12)
            throw new ArgumentException("Analiz ayı 1 ile 12 arasında olmalıdır.");

        var start = normalizedPeriod switch
        {
            "today" => today,
            "month" => new DateTime(selectedYear, selectedMonth, 1, 0, 0, 0, DateTimeKind.Utc),
            "year" => new DateTime(selectedYear, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            _ => today.AddDays(-6)
        };
        var end = normalizedPeriod switch
        {
            "today" => today.AddDays(1),
            "month" => start.AddMonths(1),
            "year" => start.AddYears(1),
            _ => today.AddDays(1)
        };
        var applications = await dbContext.CardApplications
            .AsNoTracking()
            .Include(x => x.CardType)
            .Include(x => x.CreatedByUser)
            .ToListAsync(cancellationToken);
        var periodApplications = applications
            .Where(x => x.CreatedAtUtc >= start && x.CreatedAtUtc < end)
            .ToList();
        var processed = periodApplications
            .Where(x => x.Status is ApplicationStatus.Approved or ApplicationStatus.Rejected)
            .ToList();
        var approved = processed.Count(x => x.Status == ApplicationStatus.Approved);
        var rejected = processed.Count(x => x.Status == ApplicationStatus.Rejected);
        var total = periodApplications.Count;

        var cardDistribution = periodApplications
            .GroupBy(x => GetCardTier(x.CardType.Name))
            .OrderBy(x => x.Key)
            .Select(x => new CardTypeDistributionResponse(
                x.Key, x.Count(), total == 0 ? 0 : Math.Round(x.Count() * 100m / total, 1)))
            .ToList();
        var culture = new System.Globalization.CultureInfo("tr-TR");
        var trend = normalizedPeriod switch
        {
            "year" => Enumerable.Range(1, 12)
                .Select(value => new DateTime(selectedYear, value, 1, 0, 0, 0, DateTimeKind.Utc))
                .Select(value => new ApplicationTrendResponse(
                    culture.DateTimeFormat.GetAbbreviatedMonthName(value.Month),
                    applications.Count(x => x.CreatedAtUtc.Year == value.Year && x.CreatedAtUtc.Month == value.Month)))
                .ToList(),
            "month" => Enumerable.Range(1, DateTime.DaysInMonth(selectedYear, selectedMonth))
                .Select(day => new DateTime(selectedYear, selectedMonth, day, 0, 0, 0, DateTimeKind.Utc))
                .Select(value => new ApplicationTrendResponse(
                    value.Day.ToString(), applications.Count(x => x.CreatedAtUtc.Date == value)))
                .ToList(),
            "today" => Enumerable.Range(0, 6)
                .Select(block => new ApplicationTrendResponse(
                    $"{block * 4:00}:00", applications.Count(x => x.CreatedAtUtc.Date == today
                        && x.CreatedAtUtc.Hour >= block * 4 && x.CreatedAtUtc.Hour < (block + 1) * 4)))
                .ToList(),
            _ => Enumerable.Range(0, 7)
                .Select(offset => today.AddDays(offset - 6))
                .Select(day => new ApplicationTrendResponse(
                    day.ToString("ddd", culture), applications.Count(x => x.CreatedAtUtc.Date == day)))
                .ToList()
        };
        var officerPerformance = periodApplications
            .GroupBy(x => new { x.CreatedByUserId, x.CreatedByUser.FullName })
            .Select(group =>
            {
                var decided = group.Where(x => x.Status is ApplicationStatus.Approved or ApplicationStatus.Rejected).ToList();
                var averageMinutes = decided.Count == 0
                    ? 0
                    : (int)Math.Round(decided.Average(x =>
                        ((x.EvaluatedAtUtc ?? x.CreatedAtUtc) - x.CreatedAtUtc).TotalMinutes));
                return new OfficerPerformanceResponse(
                    group.Key.FullName,
                    group.Count(),
                    decided.Count == 0 ? 0 : Math.Round(decided.Count(x => x.Status == ApplicationStatus.Approved) * 100m / decided.Count, 1),
                    averageMinutes);
            })
            .OrderByDescending(x => x.CreatedApplications)
            .ToList();

        return new ManagerDashboardResponse(
            applications.Count(x => x.Status == ApplicationStatus.Pending),
            applications.Count(x => x.Status == ApplicationStatus.Approved && x.EvaluatedAtUtc >= today),
            applications.Count(x => x.Status == ApplicationStatus.Rejected && x.EvaluatedAtUtc >= today),
            applications.Count(x => x.Status == ApplicationStatus.Revision),
            processed.Count,
            approved,
            rejected,
            processed.Count == 0 ? 0 : Math.Round(approved * 100m / processed.Count, 1),
            processed.Count == 0 ? 0 : Math.Round(rejected * 100m / processed.Count, 1),
            total,
            cardDistribution,
            trend,
            officerPerformance);
    }

    private static string GetCardTier(string cardTypeName)
    {
        if (cardTypeName.StartsWith("Platinum Plus", StringComparison.OrdinalIgnoreCase))
            return "Platinum Plus";
        if (cardTypeName.StartsWith("Platinum", StringComparison.OrdinalIgnoreCase))
            return "Platinum";
        if (cardTypeName.StartsWith("Gold", StringComparison.OrdinalIgnoreCase))
            return "Gold";
        return "Classic";
    }
}
