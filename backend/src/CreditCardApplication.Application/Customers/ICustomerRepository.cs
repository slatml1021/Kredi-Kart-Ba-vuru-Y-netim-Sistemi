using CreditCardApplication.Domain.Entities;

namespace CreditCardApplication.Application.Customers;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<Customer?> GetDetailByIdAsync(int id, CancellationToken cancellationToken);
    Task<Customer?> GetTrackedByIdAsync(int id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Customer>> SearchAsync(string query, string? criterion, CancellationToken cancellationToken);
    Task<bool> NationalIdentityNumberExistsAsync(string nationalIdentityNumber, CancellationToken cancellationToken);
    Task<int> GetNextCustomerSequenceAsync(CancellationToken cancellationToken);
    Task<bool> PhoneNumberExistsAsync(string countryCode, string phoneNumber, int? excludedCustomerId, CancellationToken cancellationToken);
    Task<bool> EmailAddressExistsAsync(string emailAddress, int? excludedCustomerId, CancellationToken cancellationToken);
    Task<bool> HasOpenApplicationsAsync(int customerId, CancellationToken cancellationToken);
    Task AddAsync(Customer customer, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
