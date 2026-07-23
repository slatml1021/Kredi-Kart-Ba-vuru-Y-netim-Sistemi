using CreditCardApplication.Application.Cards;
using CreditCardApplication.Domain.Entities;
using CreditCardApplication.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreditCardApplication.Infrastructure.Cards;

public sealed class CreditCardRepository(ApplicationDbContext dbContext) : ICreditCardRepository
{
    public Task<CreditCard?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        dbContext.CreditCards.AsNoTracking()
            .Include(x => x.CardApplication).ThenInclude(x => x.Customer)
            .Include(x => x.CardApplication).ThenInclude(x => x.CardType)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
}
