using CreditCardApplication.Application.Applications;
using CreditCardApplication.Domain.Entities;
using CreditCardApplication.Domain.Enums;
using CreditCardApplication.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreditCardApplication.Infrastructure.Applications;

public sealed class CardApplicationRepository(ApplicationDbContext dbContext) : ICardApplicationRepository
{
    public Task<Customer?> GetCustomerAsync(int customerId, CancellationToken cancellationToken) =>
        dbContext.Customers
            .Include(x => x.SavedAddresses)
            .FirstOrDefaultAsync(x => x.Id == customerId, cancellationToken);

    public Task<CardType?> GetCardTypeAsync(int cardTypeId, CancellationToken cancellationToken) =>
        dbContext.CardTypes.FirstOrDefaultAsync(x => x.Id == cardTypeId && x.IsActive, cancellationToken);

    public async Task<IReadOnlyList<CardType>> GetActiveCardTypesAsync(CancellationToken cancellationToken) =>
        await dbContext.CardTypes.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Id).ToListAsync(cancellationToken);

    public Task<bool> HasApprovedCardTypeAsync(int customerId, int cardTypeId, CancellationToken cancellationToken) =>
        dbContext.CardApplications.AnyAsync(x => x.CustomerId == customerId
            && x.CardTypeId == cardTypeId && x.Status == ApplicationStatus.Approved
            && x.CreditCard != null, cancellationToken);

    public Task<CardApplication?> GetRecentSameCardTypeAsync(
        int customerId, int cardTypeId, DateTime thresholdUtc, CancellationToken cancellationToken) =>
        dbContext.CardApplications.AsNoTracking()
            .Where(x => x.CustomerId == customerId && x.CardTypeId == cardTypeId && x.CreatedAtUtc >= thresholdUtc)
            .OrderByDescending(x => x.CreatedAtUtc).FirstOrDefaultAsync(cancellationToken);

    public async Task<decimal> GetApprovedCardLimitTotalAsync(int customerId, CancellationToken cancellationToken)
    {
        var limits = await dbContext.CreditCards
            .Where(x => x.CardApplication.CustomerId == customerId
                && x.CardApplication.Status == ApplicationStatus.Approved)
            .Select(x => x.CardLimit)
            .ToListAsync(cancellationToken);
        return limits.Sum();
    }

    public async Task<bool> HasCurrentGrantedConsentAsync(
        int customerId, string consentType, CancellationToken cancellationToken)
    {
        var current = await dbContext.CustomerConsents.AsNoTracking()
            .Where(x => x.CustomerId == customerId && x.ConsentType == consentType)
            .OrderByDescending(x => x.CapturedAtUtc)
            .Select(x => (bool?)x.IsGranted)
            .FirstOrDefaultAsync(cancellationToken);
        return current == true;
    }

    public async Task<bool> HasRequiredDocumentsAsync(int applicationId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var types = await dbContext.GeneratedDocuments.AsNoTracking()
            .Where(x => x.EntityType == "CardApplicationUpload" && x.EntityId == applicationId
                && (x.VerificationStatus == "PendingVerification" || x.VerificationStatus == "Verified")
                && (!x.ExpiresAtUtc.HasValue || x.ExpiresAtUtc > now))
            .Select(x => x.DocumentType)
            .Distinct()
            .ToListAsync(cancellationToken);
        return new[] { "Identity", "Income", "Residence" }
            .All(required => types.Contains(required, StringComparer.OrdinalIgnoreCase));
    }

    public Task<bool> HasEarlierOpenApplicationAsync(
        int customerId,
        int applicationId,
        DateTime createdAtUtc,
        CancellationToken cancellationToken) =>
        dbContext.CardApplications.AnyAsync(x => x.CustomerId == customerId
            && x.Id != applicationId
            && (x.Status == ApplicationStatus.Pending || x.Status == ApplicationStatus.Revision)
            && (x.CreatedAtUtc < createdAtUtc || (x.CreatedAtUtc == createdAtUtc && x.Id < applicationId)),
            cancellationToken);

    public Task<CardApplication?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        dbContext.CardApplications.AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.CardType)
            .Include(x => x.AssignedOfficerUser)
            .Include(x => x.FirstApprovedByUser)
            .Include(x => x.CancelledByUser)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> IsCreatedByAsync(int applicationId, int userId, CancellationToken cancellationToken) =>
        dbContext.CardApplications.AnyAsync(x => x.Id == applicationId
            && (x.CreatedByUserId == userId || x.AssignedOfficerUserId == userId), cancellationToken);

    public async Task<IReadOnlyList<CardApplication>> GetByOfficerAsync(int userId, CancellationToken cancellationToken) =>
        await dbContext.CardApplications.AsNoTracking()
            .Include(x => x.Customer).Include(x => x.CardType)
            .Include(x => x.AssignedOfficerUser).Include(x => x.FirstApprovedByUser)
            .Include(x => x.CancelledByUser)
            .Where(x => x.CreatedByUserId == userId || x.AssignedOfficerUserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CardApplication>> GetPendingAsync(CancellationToken cancellationToken) =>
        await dbContext.CardApplications.AsNoTracking()
            .Include(x => x.Customer).Include(x => x.CardType)
            .Include(x => x.AssignedOfficerUser).Include(x => x.FirstApprovedByUser)
            .Include(x => x.CancelledByUser)
            .Where(x => x.Status == ApplicationStatus.Pending)
            .OrderBy(x => x.CreatedAtUtc).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CardApplication>> GetAllAsync(CancellationToken cancellationToken) =>
        await dbContext.CardApplications.AsNoTracking()
            .Include(x => x.Customer).Include(x => x.CardType)
            .Include(x => x.AssignedOfficerUser).Include(x => x.FirstApprovedByUser)
            .Include(x => x.CancelledByUser)
            .OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken);

    public Task<CardApplication?> GetTrackedByIdAsync(int id, CancellationToken cancellationToken) =>
        dbContext.CardApplications
            .Include(x => x.Customer)
            .Include(x => x.CardType)
            .Include(x => x.CreditCard)
            .Include(x => x.RevisionSnapshots)
            .Include(x => x.AssignedOfficerUser)
            .Include(x => x.FirstApprovedByUser)
            .Include(x => x.CancelledByUser)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<CardApplication?> GetDetailByIdAsync(int id, CancellationToken cancellationToken) =>
        dbContext.CardApplications.AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.CardType)
            .Include(x => x.CreditCard)
            .Include(x => x.AssignedOfficerUser)
            .Include(x => x.FirstApprovedByUser)
            .Include(x => x.CancelledByUser)
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
