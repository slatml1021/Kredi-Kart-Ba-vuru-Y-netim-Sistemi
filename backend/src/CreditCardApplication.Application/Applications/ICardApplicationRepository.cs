using CreditCardApplication.Domain.Entities;

namespace CreditCardApplication.Application.Applications;

public interface ICardApplicationRepository
{
    Task<Customer?> GetCustomerAsync(int customerId, CancellationToken cancellationToken);
    Task<CardType?> GetCardTypeAsync(int cardTypeId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CardType>> GetActiveCardTypesAsync(CancellationToken cancellationToken);
    Task<bool> HasOpenApplicationAsync(int customerId, CancellationToken cancellationToken);
    Task<CardApplication?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<bool> IsCreatedByAsync(int applicationId, int userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CardApplication>> GetByOfficerAsync(int userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CardApplication>> GetPendingAsync(CancellationToken cancellationToken);
    Task<CardApplication?> GetTrackedByIdAsync(int id, CancellationToken cancellationToken);
    Task<CardApplication?> GetDetailByIdAsync(int id, CancellationToken cancellationToken);
    Task AddAsync(CardApplication application, ApplicationHistory history, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
