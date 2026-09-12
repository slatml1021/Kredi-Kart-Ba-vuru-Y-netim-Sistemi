using CreditCardApplication.Application.Cards;
using CreditCardApplication.Domain.Entities;
using CreditCardApplication.Infrastructure.Persistence;
using CreditCardApplication.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CreditCardApplication.Infrastructure.Cards;

public sealed class CreditCardRepository(ApplicationDbContext dbContext) : ICreditCardRepository
{
    public Task<CreditCard?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        dbContext.CreditCards.AsNoTracking()
            .Include(x => x.CardApplication).ThenInclude(x => x.Customer)
            .Include(x => x.CardApplication).ThenInclude(x => x.CardType)
            .Include(x => x.Fulfillment)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<CreditCard?> GetTrackedByIdAsync(int id, CancellationToken cancellationToken) =>
        dbContext.CreditCards
            .Include(x => x.CardApplication).ThenInclude(x => x.Customer)
            .Include(x => x.CardApplication).ThenInclude(x => x.CardType)
            .Include(x => x.Fulfillment)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<CreditCard>> GetLimitIncreaseRequestsAsync(
        int? officerUserId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.CreditCards.AsNoTracking()
            .Where(x => x.LimitIncreaseStatus != null);
        if (officerUserId.HasValue)
            query = query.Where(x => x.CardApplication.CreatedByUserId == officerUserId.Value);

        return await query
            .Include(x => x.CardApplication).ThenInclude(x => x.Customer)
            .Include(x => x.CardApplication).ThenInclude(x => x.CardType)
            .OrderBy(x => x.LimitIncreaseStatus == "Pending" ? 0 : 1)
            .ThenBy(x => x.LimitIncreaseRequestedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<decimal> GetOtherApprovedCardLimitAsync(
        int customerId,
        int excludedCardId,
        CancellationToken cancellationToken)
    {
        var limits = await dbContext.CreditCards
            .Where(x => x.Id != excludedCardId
                && x.CardApplication.CustomerId == customerId
                && x.CardApplication.Status == ApplicationStatus.Approved)
            .Select(x => x.CardLimit)
            .ToListAsync(cancellationToken);
        return limits.Sum();
    }

    public async Task<decimal> GetReservedSupplementaryLimitAsync(
        int cardId, CancellationToken cancellationToken)
    {
        var limits = await dbContext.SupplementaryCardApplications.AsNoTracking()
            .Where(x => x.PrimaryCreditCardId == cardId
                        && (x.Status == "Pending" || x.Status == "Approved"))
            .Select(x => x.RequestedLimit)
            .ToListAsync(cancellationToken);
        return limits.Sum();
    }

    public Task AddNotificationAsync(
        int userId, string title, string message, string link, CancellationToken cancellationToken)
    {
        dbContext.Notifications.Add(new Notification
        {
            UserId = userId, Type = "Info", Title = title, Message = message, Link = link,
            CreatedAtUtc = DateTime.UtcNow
        });
        return Task.CompletedTask;
    }

    public async Task AddRoleNotificationsAsync(
        string role, string title, string message, string link, CancellationToken cancellationToken)
    {
        var userIds = await dbContext.UserRoles.AsNoTracking()
            .Where(x => x.Role.Name == role && x.User.IsActive)
            .Select(x => x.UserId).Distinct().ToListAsync(cancellationToken);
        foreach (var userId in userIds)
            await AddNotificationAsync(userId, title, message, link, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
