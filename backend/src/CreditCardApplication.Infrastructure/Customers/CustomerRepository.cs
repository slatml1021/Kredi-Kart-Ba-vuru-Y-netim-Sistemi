using CreditCardApplication.Application.Customers;
using CreditCardApplication.Domain.Entities;
using CreditCardApplication.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreditCardApplication.Infrastructure.Customers;

public sealed class CustomerRepository(ApplicationDbContext dbContext) : ICustomerRepository
{
    public Task<Customer?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        dbContext.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Customer?> GetTrackedByIdAsync(int id, CancellationToken cancellationToken) =>
        dbContext.Customers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Customer>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var normalized = query.ToUpperInvariant();
        return await dbContext.Customers
            .AsNoTracking()
            .Where(x => x.NationalIdentityNumber == query
                || x.PhoneNumber == query
                || x.CustomerNumber.ToUpper() == normalized
                || (x.FirstName + " " + x.LastName).ToUpper().Contains(normalized))
            .OrderBy(x => x.FirstName)
            .ThenBy(x => x.LastName)
            .Take(25)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> NationalIdentityNumberExistsAsync(
        string nationalIdentityNumber,
        CancellationToken cancellationToken) =>
        dbContext.Customers.AnyAsync(x => x.NationalIdentityNumber == nationalIdentityNumber, cancellationToken);

    public async Task AddAsync(Customer customer, CancellationToken cancellationToken)
    {
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
