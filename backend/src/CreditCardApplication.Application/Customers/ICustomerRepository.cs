using CreditCardApplication.Domain.Entities;

namespace CreditCardApplication.Application.Customers;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<Customer?> GetTrackedByIdAsync(int id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Customer>> SearchAsync(string query, CancellationToken cancellationToken);
    Task<bool> NationalIdentityNumberExistsAsync(string nationalIdentityNumber, CancellationToken cancellationToken);
    Task AddAsync(Customer customer, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
