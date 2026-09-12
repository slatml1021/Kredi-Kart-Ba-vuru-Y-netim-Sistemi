using CreditCardApplication.Domain.Entities;

namespace CreditCardApplication.Application.Cards;

public interface ICreditCardRepository
{
    Task<CreditCard?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<CreditCard?> GetTrackedByIdAsync(int id, CancellationToken cancellationToken);
    Task<IReadOnlyList<CreditCard>> GetLimitIncreaseRequestsAsync(int? officerUserId, CancellationToken cancellationToken);
    Task<decimal> GetOtherApprovedCardLimitAsync(int customerId, int excludedCardId, CancellationToken cancellationToken);
    Task<decimal> GetReservedSupplementaryLimitAsync(int cardId, CancellationToken cancellationToken);
    Task AddNotificationAsync(int userId, string title, string message, string link, CancellationToken cancellationToken);
    Task AddRoleNotificationsAsync(string role, string title, string message, string link, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
