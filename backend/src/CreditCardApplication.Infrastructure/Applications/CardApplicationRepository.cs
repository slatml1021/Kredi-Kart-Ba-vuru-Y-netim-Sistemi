using CreditCardApplication.Application.Applications;
using CreditCardApplication.Domain.Entities;
using CreditCardApplication.Domain.Enums;
using CreditCardApplication.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreditCardApplication.Infrastructure.Applications;

public sealed class CardApplicationRepository(ApplicationDbContext dbContext) : ICardApplicationRepository
{
    public Task<Customer?> GetCustomerAsync(int customerId, CancellationToken cancellationToken) =>
        dbContext.Customers.FirstOrDefaultAsync(x => x.Id == customerId, cancellationToken);

    public Task<CardType?> GetCardTypeAsync(int cardTypeId, CancellationToken cancellationToken) =>
        dbContext.CardTypes.FirstOrDefaultAsync(x => x.Id == cardTypeId && x.IsActive, cancellationToken);

    public async Task<IReadOnlyList<CardType>> GetActiveCardTypesAsync(CancellationToken cancellationToken) =>
        await dbContext.CardTypes.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Id).ToListAsync(cancellationToken);

    public Task<bool> HasOpenApplicationAsync(int customerId, CancellationToken cancellationToken) =>
        dbContext.CardApplications.AnyAsync(x => x.CustomerId == customerId
            && (x.Status == ApplicationStatus.Pending || x.Status == ApplicationStatus.Revision), cancellationToken);

    public Task<CardApplication?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        dbContext.CardApplications.AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.CardType)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> IsCreatedByAsync(int applicationId, int userId, CancellationToken cancellationToken) =>
        dbContext.CardApplications.AnyAsync(x => x.Id == applicationId && x.CreatedByUserId == userId, cancellationToken);

    public async Task<IReadOnlyList<CardApplication>> GetByOfficerAsync(int userId, CancellationToken cancellationToken) =>
        await dbContext.CardApplications.AsNoTracking()
            .Include(x => x.Customer).Include(x => x.CardType)
            .Where(x => x.CreatedByUserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CardApplication>> GetPendingAsync(CancellationToken cancellationToken) =>
        await dbContext.CardApplications.AsNoTracking()
            .Include(x => x.Customer).Include(x => x.CardType)
            .Where(x => x.Status == ApplicationStatus.Pending)
            .OrderBy(x => x.CreatedAtUtc).ToListAsync(cancellationToken);

    public Task<CardApplication?> GetTrackedByIdAsync(int id, CancellationToken cancellationToken) =>
        dbContext.CardApplications
            .Include(x => x.Customer)
            .Include(x => x.CardType)
            .Include(x => x.CreditCard)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<CardApplication?> GetDetailByIdAsync(int id, CancellationToken cancellationToken) =>
        dbContext.CardApplications.AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.CardType)
            .Include(x => x.CreditCard)
            .Include(x => x.Histories).ThenInclude(x => x.ChangedByUser)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task AddAsync(
        CardApplication application,
        ApplicationHistory history,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.CardApplications.Add(application);
        dbContext.ApplicationHistories.Add(history);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
