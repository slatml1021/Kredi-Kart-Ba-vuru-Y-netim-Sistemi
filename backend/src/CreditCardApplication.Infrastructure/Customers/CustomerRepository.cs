using CreditCardApplication.Application.Customers;
using CreditCardApplication.Domain.Entities;
using CreditCardApplication.Domain.Enums;
using CreditCardApplication.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreditCardApplication.Infrastructure.Customers;

public sealed class CustomerRepository(ApplicationDbContext dbContext) : ICustomerRepository
{
    public Task<Customer?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        dbContext.Customers.AsNoTracking()
            .Include(x => x.OtherBankCards)
            .Include(x => x.SavedAddresses)
            .Include(x => x.Applications).ThenInclude(x => x.CreditCard)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Customer?> GetDetailByIdAsync(int id, CancellationToken cancellationToken) =>
        dbContext.Customers.AsNoTracking()
            .Include(x => x.Applications).ThenInclude(x => x.CardType)
            .Include(x => x.Applications).ThenInclude(x => x.CreditCard)
            .Include(x => x.OtherBankCards)
            .Include(x => x.SavedAddresses)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Customer?> GetTrackedByIdAsync(int id, CancellationToken cancellationToken) =>
        dbContext.Customers
            .Include(x => x.OtherBankCards)
            .Include(x => x.SavedAddresses)
            .Include(x => x.Applications).ThenInclude(x => x.CreditCard)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken cancellationToken) =>
        await dbContext.Customers.AsNoTracking()
            .Include(x => x.OtherBankCards)
            .Include(x => x.SavedAddresses)
            .Include(x => x.Applications).ThenInclude(x => x.CreditCard)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenBy(x => x.FirstName)
            .ThenBy(x => x.LastName)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Customer>> SearchAsync(string query, string? criterion, CancellationToken cancellationToken)
    {
        var normalized = query.ToUpperInvariant();
        var customers = dbContext.Customers.AsNoTracking();
        customers = criterion?.ToLowerInvariant() switch
        {
            "nationalidentitynumber" or "identity" => customers.Where(x => x.NationalIdentityNumber == query),
            "customernumber" or "customer" => customers.Where(x => x.CustomerNumber.ToUpper() == normalized),
            "phonenumber" or "phone" => customers.Where(x => x.PhoneNumber == query
                || x.PhoneCountryCode.Replace("+", "") + x.PhoneNumber == query),
            _ => customers.Where(x => x.NationalIdentityNumber == query
                || x.PhoneNumber == query
                || x.CustomerNumber.ToUpper() == normalized
                || (x.FirstName + " " + x.LastName).ToUpper().Contains(normalized))
        };
        return await customers
            .Include(x => x.OtherBankCards)
            .Include(x => x.SavedAddresses)
            .Include(x => x.Applications).ThenInclude(x => x.CreditCard)
            .OrderBy(x => x.FirstName)
            .ThenBy(x => x.LastName)
            .Take(25)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> NationalIdentityNumberExistsAsync(
        string nationalIdentityNumber,
        CancellationToken cancellationToken) =>
        dbContext.Customers.AnyAsync(x => x.NationalIdentityNumber == nationalIdentityNumber, cancellationToken);

    public async Task<int> GetNextCustomerSequenceAsync(CancellationToken cancellationToken)
    {
        var numbers = await dbContext.Customers.AsNoTracking()
            .Where(x => x.CustomerNumber.StartsWith("MUS"))
            .Select(x => x.CustomerNumber)
            .ToListAsync(cancellationToken);
        return numbers.Select(value => int.TryParse(value[3..], out var number) ? number : 0)
            .DefaultIfEmpty().Max() + 1;
    }

    public Task<bool> PhoneNumberExistsAsync(
        string countryCode,
        string phoneNumber,
        int? excludedCustomerId,
        CancellationToken cancellationToken) =>
        dbContext.Customers.AnyAsync(x =>
            x.PhoneCountryCode == countryCode
            && x.PhoneNumber == phoneNumber
            && (!excludedCustomerId.HasValue || x.Id != excludedCustomerId.Value), cancellationToken);

    public Task<bool> EmailAddressExistsAsync(
        string emailAddress,
        int? excludedCustomerId,
        CancellationToken cancellationToken) =>
        dbContext.Customers.AnyAsync(x =>
            x.EmailAddress == emailAddress
            && (!excludedCustomerId.HasValue || x.Id != excludedCustomerId.Value), cancellationToken);

    public Task<bool> HasOpenApplicationsAsync(int customerId, CancellationToken cancellationToken) =>
        dbContext.CardApplications.AnyAsync(x => x.CustomerId == customerId
            && (x.Status == ApplicationStatus.Pending || x.Status == ApplicationStatus.Revision), cancellationToken);

    public async Task AddAsync(Customer customer, CancellationToken cancellationToken)
    {
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
