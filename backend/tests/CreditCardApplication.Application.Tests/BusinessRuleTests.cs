using CreditCardApplication.Application.Applications;
using CreditCardApplication.Application.Services;
using CreditCardApplication.Domain.Entities;
using CreditCardApplication.Domain.Enums;
using Xunit;

namespace CreditCardApplication.Application.Tests;

public sealed class BusinessRuleTests
{
    [Fact]
    public void LimitCalculator_SubtractsOtherBankLimitFromThreeTimesIncome()
    {
        var result = new LimitCalculator().Calculate(20_000m, 10_000m);
        Assert.Equal(60_000m, result.TheoreticalTotalLimit);
        Assert.Equal(50_000m, result.AvailableLimit);
    }

    [Fact]
    public async Task Create_RejectsLimitAboveAvailableLimit()
    {
        var service = CreateService(out _);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new CreateCardApplicationRequest(10, 1, 50_001m), 1, default));
        Assert.Contains("kullanılabilir limit", exception.Message);
    }

    [Fact]
    public async Task Create_RejectsSecondOpenApplicationForCustomer()
    {
        var service = CreateService(out var repository);
        repository.HasOpenApplication = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new CreateCardApplicationRequest(10, 1, 20_000m), 1, default));
    }

    [Fact]
    public async Task Revision_RequiresExplanation()
    {
        var service = CreateService(out var repository);
        repository.TrackedApplication = CreatePendingApplication();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.EvaluateAsync(1, new EvaluateCardApplicationRequest("Revision", null, " "), 2, default));
    }

    [Fact]
    public async Task Resubmit_RejectsDifferentOfficer()
    {
        var service = CreateService(out var repository);
        repository.TrackedApplication = CreatePendingApplication();
        repository.TrackedApplication.Status = ApplicationStatus.Revision;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.ResubmitAsync(1, new ResubmitCardApplicationRequest(1, 20_000m), 99, default));
    }

    [Fact]
    public async Task Approval_CreatesSingleInactiveMaskedCardAndFinalizesApplication()
    {
        var service = CreateService(out var repository);
        repository.TrackedApplication = CreatePendingApplication();
        var response = await service.EvaluateAsync(1,
            new EvaluateCardApplicationRequest("Approved", 20_000m, "Uygun"), 2, default);

        Assert.Equal("Approved", response.Status);
        Assert.Equal(ApplicationStatus.Approved, repository.TrackedApplication.Status);
        Assert.NotNull(repository.TrackedApplication.CreditCard);
        Assert.Equal(CardStatus.Inactive, repository.TrackedApplication.CreditCard!.Status);
        Assert.Matches(@"^45000101 \*\*\*\* \d{4}$", repository.TrackedApplication.CreditCard.MaskedCardNumber);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.EvaluateAsync(1,
            new EvaluateCardApplicationRequest("Approved", 20_000m, null), 2, default));
    }

    private static CardApplicationService CreateService(out FakeCardApplicationRepository repository)
    {
        repository = new FakeCardApplicationRepository();
        return new CardApplicationService(repository, new LimitCalculator());
    }

    private static CardApplication CreatePendingApplication()
    {
        var customer = FakeCardApplicationRepository.CreateCustomer();
        var cardType = FakeCardApplicationRepository.CreateCardType();
        return new CardApplication
        {
            Id = 1, ApplicationNumber = "KKB-TEST-0001", CustomerId = customer.Id, Customer = customer,
            CardTypeId = cardType.Id, CardType = cardType, CreatedByUserId = 1,
            RequestedLimit = 20_000m, Status = ApplicationStatus.Pending
        };
    }
}

internal sealed class FakeCardApplicationRepository : ICardApplicationRepository
{
    public bool HasOpenApplication { get; set; }
    public CardApplication? TrackedApplication { get; set; }

    public static Customer CreateCustomer() => new()
    {
        Id = 10, CustomerNumber = "CUS-TEST", NationalIdentityNumber = "12345678901",
        FirstName = "Test", LastName = "Müşteri", PhoneNumber = "05555555555",
        EmailAddress = "test@example.com", MonthlyNetIncome = 20_000m,
        OtherBankTotalCardLimit = 10_000m, IsActive = true
    };

    public static CardType CreateCardType() => new()
    {
        Id = 1, Name = "Classic", Bin = "45000101", MinimumLimit = 5_000m,
        MaximumLimit = 50_000m, IsActive = true
    };

    public Task<Customer?> GetCustomerAsync(int customerId, CancellationToken cancellationToken) => Task.FromResult<Customer?>(CreateCustomer());
    public Task<CardType?> GetCardTypeAsync(int cardTypeId, CancellationToken cancellationToken) => Task.FromResult<CardType?>(CreateCardType());
    public Task<IReadOnlyList<CardType>> GetActiveCardTypesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<CardType>>([CreateCardType()]);
    public Task<bool> HasOpenApplicationAsync(int customerId, CancellationToken cancellationToken) => Task.FromResult(HasOpenApplication);
    public Task<CardApplication?> GetByIdAsync(int id, CancellationToken cancellationToken) => Task.FromResult(TrackedApplication);
    public Task<bool> IsCreatedByAsync(int applicationId, int userId, CancellationToken cancellationToken) => Task.FromResult(TrackedApplication?.CreatedByUserId == userId);
    public Task<IReadOnlyList<CardApplication>> GetByOfficerAsync(int userId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<CardApplication>>([]);
    public Task<IReadOnlyList<CardApplication>> GetPendingAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<CardApplication>>([]);
    public Task<CardApplication?> GetTrackedByIdAsync(int id, CancellationToken cancellationToken) => Task.FromResult(TrackedApplication);
    public Task<CardApplication?> GetDetailByIdAsync(int id, CancellationToken cancellationToken) => Task.FromResult(TrackedApplication);
    public Task AddAsync(CardApplication application, ApplicationHistory history, CancellationToken cancellationToken)
    {
        application.Id = 1;
        TrackedApplication = application;
        return Task.CompletedTask;
    }
    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
