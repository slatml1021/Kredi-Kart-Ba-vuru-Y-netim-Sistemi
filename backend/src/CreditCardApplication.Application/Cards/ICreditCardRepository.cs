using CreditCardApplication.Domain.Entities;

namespace CreditCardApplication.Application.Cards;

public interface ICreditCardRepository
{
    Task<CreditCard?> GetByIdAsync(int id, CancellationToken cancellationToken);
}
