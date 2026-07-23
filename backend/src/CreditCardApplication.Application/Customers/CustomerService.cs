using System.Net.Mail;
using CreditCardApplication.Domain.Entities;

namespace CreditCardApplication.Application.Customers;

public sealed class CustomerService(ICustomerRepository customerRepository)
{
    public async Task<CustomerResponse> CreateAsync(
        CreateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);

        if (await customerRepository.NationalIdentityNumberExistsAsync(request.NationalIdentityNumber, cancellationToken))
        {
            throw new InvalidOperationException("Bu TC Kimlik Numarası ile kayıtlı bir müşteri bulunmaktadır.");
        }

        var customer = new Customer
        {
            CustomerNumber = $"CUS-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            NationalIdentityNumber = request.NationalIdentityNumber,
            FirstName = NormalizeName(request.FirstName),
            LastName = NormalizeName(request.LastName),
            PhoneNumber = request.PhoneNumber,
            EmailAddress = request.EmailAddress.Trim().ToLowerInvariant(),
            MonthlyNetIncome = request.MonthlyNetIncome,
            OtherBankTotalCardLimit = request.OtherBankTotalCardLimit,
            IsActive = true
        };

        await customerRepository.AddAsync(customer, cancellationToken);
        return Map(customer);
    }

    public async Task<CustomerResponse?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var customer = await customerRepository.GetByIdAsync(id, cancellationToken);
        return customer is null ? null : Map(customer);
    }

    public async Task<CustomerResponse> UpdateAsync(int id, UpdateCustomerRequest request, CancellationToken cancellationToken)
    {
        Validate(request);
        var customer = await customerRepository.GetTrackedByIdAsync(id, cancellationToken)
            ?? throw new ArgumentException("Müşteri bulunamadı.");
        customer.FirstName = NormalizeName(request.FirstName);
        customer.LastName = NormalizeName(request.LastName);
        customer.PhoneNumber = request.PhoneNumber;
        customer.EmailAddress = request.EmailAddress.Trim().ToLowerInvariant();
        customer.MonthlyNetIncome = request.MonthlyNetIncome;
        customer.OtherBankTotalCardLimit = request.OtherBankTotalCardLimit;
        customer.UpdatedAtUtc = DateTime.UtcNow;
        await customerRepository.SaveChangesAsync(cancellationToken);
        return Map(customer);
    }

    public async Task<IReadOnlyList<CustomerResponse>> SearchAsync(
        string query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("En az bir arama kriteri girilmelidir.", nameof(query));
        }

        var customers = await customerRepository.SearchAsync(query.Trim(), cancellationToken);
        return customers.Select(Map).ToList();
    }

    private static void Validate(CreateCustomerRequest request)
    {
        if (request.NationalIdentityNumber.Length != 11 || !request.NationalIdentityNumber.All(char.IsDigit))
            throw new ArgumentException("TC Kimlik Numarası 11 haneli ve yalnızca rakamlardan oluşmalıdır.");

        ValidateCommon(request.FirstName, request.LastName, request.PhoneNumber, request.EmailAddress, request.MonthlyNetIncome, request.OtherBankTotalCardLimit);
    }

    private static void Validate(UpdateCustomerRequest request) =>
        ValidateCommon(request.FirstName, request.LastName, request.PhoneNumber, request.EmailAddress, request.MonthlyNetIncome, request.OtherBankTotalCardLimit);

    private static void ValidateCommon(string firstName, string lastName, string phoneNumber, string emailAddress, decimal monthlyNetIncome, decimal otherBankTotalCardLimit)
    {
        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            throw new ArgumentException("Ad ve soyad alanları zorunludur.");

        if (phoneNumber.Length != 11 || !phoneNumber.All(char.IsDigit))
            throw new ArgumentException("Telefon numarası 11 haneli ve yalnızca rakamlardan oluşmalıdır.");

        try { _ = new MailAddress(emailAddress); }
        catch { throw new ArgumentException("Geçerli bir e-posta adresi girilmelidir."); }

        if (monthlyNetIncome <= 0)
            throw new ArgumentException("Aylık net gelir sıfırdan büyük olmalıdır.");

        if (otherBankTotalCardLimit < 0)
            throw new ArgumentException("Diğer banka toplam kart limiti negatif olamaz.");
    }

    private static string NormalizeName(string value) => value.Trim();

    private static CustomerResponse Map(Customer customer) => new(
        customer.Id,
        customer.CustomerNumber,
        customer.NationalIdentityNumber,
        customer.FirstName,
        customer.LastName,
        customer.PhoneNumber,
        customer.EmailAddress,
        customer.MonthlyNetIncome,
        customer.OtherBankTotalCardLimit,
        customer.IsActive);
}
