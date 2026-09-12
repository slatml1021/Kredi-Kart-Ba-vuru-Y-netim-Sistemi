using CreditCardApplication.Application.Applications;
using CreditCardApplication.Application.Customers;
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
    public void LimitCalculator_SubtractsOwnBankCardsAsWell()
    {
        var result = new LimitCalculator().Calculate(30_000m, 10_000m, 25_000m);
        Assert.Equal(55_000m, result.AvailableLimit);
    }

    [Fact]
    public async Task Create_RequiresCurrentKvkkConsent()
    {
        var service = CreateService(out var repository);
        repository.HasKvkkConsent = false;
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(
            new CreateCardApplicationRequest(10, 1, 20_000m,
                IdentityDocumentConfirmed: true, IncomeDocumentConfirmed: true,
                ResidenceDocumentConfirmed: true), 1, default));
        Assert.Contains("KVKK", exception.Message);
    }

    [Fact]
    public async Task Create_AcceptsRequestedLimitAboveAvailableLimitForManagerDecision()
    {
        var service = CreateService(out var repository);
        var result = await service.CreateAsync(new CreateCardApplicationRequest(
            10, 1, 50_001m, IdentityDocumentConfirmed: true,
            IncomeDocumentConfirmed: true, ResidenceDocumentConfirmed: true), 1, default);
        Assert.Equal("Pending", result.Status);
        Assert.Equal(50_001m, repository.TrackedApplication!.RequestedLimit);
    }

    [Fact]
    public async Task Create_AllowsSecondOpenApplicationForCustomerQueue()
    {
        var service = CreateService(out var repository);
        repository.HasOpenApplication = true;
        var result = await service.CreateAsync(
            new CreateCardApplicationRequest(10, 1, 20_000m,
                IdentityDocumentConfirmed: true, IncomeDocumentConfirmed: true,
                ResidenceDocumentConfirmed: true), 1, default);
        Assert.Equal("Pending", result.Status);
    }

    [Fact]
    public async Task Create_RequiresValidDeliveryAddress()
    {
        var service = CreateService(out _);
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(new CreateCardApplicationRequest(
                10, 1, 20_000m, "Kısa", "Email", "DifferentAddress",
                "İstanbul", "Kadıköy", "Caferağa", "Test Müşteri", "05555555555"), 1, default));
        Assert.Contains("teslimat adresi", exception.Message);
    }

    [Fact]
    public async Task Create_PersistsDeliveryAndStatementPreference()
    {
        var service = CreateService(out var repository);
        var response = await service.CreateAsync(new CreateCardApplicationRequest(
            10, 1, 20_000m, "Atatürk Mahallesi No: 10 İstanbul", "Paper", "DifferentAddress",
            "İstanbul", "Kadıköy", "Caferağa", "Test Müşteri", "05555555555",
            IdentityDocumentConfirmed: true, IncomeDocumentConfirmed: true,
            ResidenceDocumentConfirmed: true), 1, default);

        Assert.Equal("Atatürk Mahallesi No: 10 İstanbul", response.DeliveryAddress);
        Assert.Equal("Paper", response.StatementPreference);
        Assert.Equal("Paper", repository.TrackedApplication!.StatementPreference);
    }

    [Fact]
    public async Task Create_PersistsBranchDeliveryAndCardPreferences()
    {
        var service = CreateService(out var repository);
        var response = await service.CreateAsync(new CreateCardApplicationRequest(
            10, 1, 20_000m, "Finanskent Şubesi — Ümraniye", "Mobile",
            "Branch", DeliveryBranch: "Finanskent Şubesi — Ümraniye",
            ContactlessEnabled: false, InternetShoppingEnabled: true,
            ApplicationNote: "Müşteri şubeden teslim alacak.",
            IdentityDocumentConfirmed: true, IncomeDocumentConfirmed: true,
            ResidenceDocumentConfirmed: true), 1, default);

        Assert.Equal("Branch", response.DeliveryMethod);
        Assert.Equal("Mobile", response.StatementPreference);
        Assert.False(response.ContactlessEnabled);
        Assert.True(response.InternetShoppingEnabled);
        Assert.Equal("Finanskent Şubesi — Ümraniye", repository.TrackedApplication!.DeliveryBranch);
    }

    [Fact]
    public async Task Create_RequiresCompleteDifferentDeliveryAddress()
    {
        var service = CreateService(out _);
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(new CreateCardApplicationRequest(
                10, 1, 20_000m, "Atatürk Mahallesi No: 10", "Email",
                "DifferentAddress", DeliveryCity: "İstanbul"), 1, default));

        Assert.Contains("il, ilçe", exception.Message);
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
    public async Task Approval_RequiresUploadedIdentityIncomeAndResidenceDocuments()
    {
        var service = CreateService(out var repository);
        repository.TrackedApplication = CreatePendingApplication();
        repository.HasRequiredDocuments = false;
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.EvaluateAsync(
            1, new EvaluateCardApplicationRequest("Approved", 15_000m, null), 2, default));
        Assert.Contains("sisteme yüklenmelidir", exception.Message);
    }

    [Fact]
    public async Task Rejection_RequiresExplanation()
    {
        var service = CreateService(out var repository);
        repository.TrackedApplication = CreatePendingApplication();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.EvaluateAsync(1, new EvaluateCardApplicationRequest("Rejected", null, " "), 2, default));
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

    [Theory]
    [InlineData("450001", 6)]
    [InlineData("45000101", 8)]
    public void PaymentCardNumberGenerator_UsesBinAndProducesValidLuhnPan(string bin, int binLength)
    {
        var pan = PaymentCardNumberGenerator.Generate(bin);

        Assert.Equal(16, pan.Length);
        Assert.StartsWith(bin, pan);
        Assert.True(PaymentCardNumberGenerator.IsValid(pan));
        Assert.Matches($"^{bin} \\*{{{16 - binLength - 4}}} \\d{{4}}$",
            PaymentCardNumberGenerator.Mask(pan, binLength));
    }

    [Fact]
    public async Task HighValueApproval_RequiresTwoDifferentManagers()
    {
        var service = CreateService(out var repository);
        repository.TrackedApplication = CreatePendingApplication();
        repository.TrackedApplication.Customer.MonthlyNetIncome = 100_000m;
        repository.TrackedApplication.Customer.OtherBankTotalCardLimit = 0m;
        repository.TrackedApplication.CardType.MaximumLimit = 500_000m;

        var firstApproval = await service.EvaluateAsync(1,
            new EvaluateCardApplicationRequest("Approved", 150_000m, "Birinci kontrol uygun."), 2, default);

        Assert.Equal("Pending", firstApproval.Status);
        Assert.Equal("SecondManagerApproval", firstApproval.WorkflowStage);
        Assert.True(repository.TrackedApplication.RequiresSecondApproval);
        Assert.Equal(2, repository.TrackedApplication.FirstApprovedByUserId);
        Assert.Null(repository.TrackedApplication.CreditCard);

        var sameManagerError = await Assert.ThrowsAsync<InvalidOperationException>(() => service.EvaluateAsync(1,
            new EvaluateCardApplicationRequest("Approved", 150_000m, "İkinci kontrol."), 2, default));
        Assert.Contains("farklı bir müdür", sameManagerError.Message);

        var finalApproval = await service.EvaluateAsync(1,
            new EvaluateCardApplicationRequest("Approved", 150_000m, "Bağımsız ikinci kontrol uygun."), 3, default);
        Assert.Equal("Approved", finalApproval.Status);
        Assert.NotNull(repository.TrackedApplication.CreditCard);
    }

    [Fact]
    public async Task SecondApproval_MustUseFirstApprovedLimit()
    {
        var service = CreateService(out var repository);
        repository.TrackedApplication = CreatePendingApplication();
        repository.TrackedApplication.Customer.MonthlyNetIncome = 100_000m;
        repository.TrackedApplication.Customer.OtherBankTotalCardLimit = 0m;
        repository.TrackedApplication.CardType.MaximumLimit = 500_000m;
        await service.EvaluateAsync(1,
            new EvaluateCardApplicationRequest("Approved", 150_000m, "Birinci kontrol."), 2, default);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.EvaluateAsync(1,
            new EvaluateCardApplicationRequest("Approved", 140_000m, "Limit değiştirildi."), 3, default));
        Assert.Contains("aynı olmalıdır", error.Message);
    }

    [Fact]
    public async Task CustomerCreate_RejectsUnderEighteen()
    {
        var service = new CustomerService(new FakeCustomerRepository());
        var request = ValidCustomerRequest() with
        {
            BirthDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-17))
        };
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(request, default));
        Assert.Contains("18 yaş", exception.Message);
    }

    [Fact]
    public async Task CustomerCreate_RejectsDuplicatePhoneAndEmail()
    {
        var repository = new FakeCustomerRepository { DuplicatePhone = true };
        var service = new CustomerService(repository);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(ValidCustomerRequest(), default));
        repository.DuplicatePhone = false;
        repository.DuplicateEmail = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(ValidCustomerRequest(), default));
    }

    [Fact]
    public async Task CustomerCreate_CalculatesOtherBankTotalAndCreditScore()
    {
        var repository = new FakeCustomerRepository();
        var service = new CustomerService(repository);
        var response = await service.CreateAsync(ValidCustomerRequest() with
        {
            OtherBankCards =
            [
                new OtherBankCardRequest("A Bankası", "1234", 10_000m),
                new OtherBankCardRequest("B Bankası", null, 5_000m)
            ]
        }, default);
        Assert.Equal(response.OtherBankCards.Sum(x => x.CardLimit), response.OtherBankTotalCardLimit);
        Assert.DoesNotContain(response.OtherBankCards, x => x.BankName is "A Bankası" or "B Bankası");
        Assert.InRange(response.CreditScore, 1, 1900);
        var tracked = await repository.GetTrackedByIdAsync(response.Id, default);
        var defaultAddress = Assert.Single(tracked!.SavedAddresses);
        Assert.Equal("Test Sokak", defaultAddress.Street);
        Assert.Equal("1", defaultAddress.BuildingNo);
        Assert.Equal("34710", defaultAddress.PostalCode);
    }

    [Fact]
    public void ExternalRiskProfile_IsDeterministicAndCalculatedOnlyByBackend()
    {
        var service = new CustomerService(new FakeCustomerRepository());

        var first = service.GetExternalRiskProfile("10000000078");
        var second = service.GetExternalRiskProfile("10000000078");

        Assert.Equal(first.OtherBankTotalCardLimit, second.OtherBankTotalCardLimit);
        Assert.Equal(first.Cards, second.Cards);
        Assert.Equal(first.Cards.Sum(x => x.CardLimit), first.OtherBankTotalCardLimit);
        Assert.All(first.Cards, card => Assert.Matches("^[0-9]{4}$", card.CardLastFourDigits));
    }

    [Fact]
    public async Task CustomerAddress_AddingDefaultAddressUpdatesPrimaryAddress()
    {
        var repository = new FakeCustomerRepository();
        var service = new CustomerService(repository);
        var customer = await service.CreateAsync(ValidCustomerRequest(), default);

        var address = await service.AddAddressAsync(customer.Id, new CustomerAddressRequest(
            "İş Adresi", "İstanbul", "Ümraniye", "Finanskent", "Büyükdere Sokak",
            null, "12", "4", "2", "34760", true), default);

        var tracked = await repository.GetTrackedByIdAsync(customer.Id, default);
        Assert.True(address.IsDefault);
        Assert.Equal("İstanbul", tracked!.City);
        Assert.Equal("Ümraniye", tracked.District);
        Assert.Contains("Büyükdere Sokak", tracked.Address);
        Assert.Single(tracked.SavedAddresses, item => item.IsDefault && item.IsActive);
    }

    [Fact]
    public async Task CustomerAddress_RejectsDuplicateActiveName()
    {
        var repository = new FakeCustomerRepository();
        var service = new CustomerService(repository);
        var customer = await service.CreateAsync(ValidCustomerRequest(), default);
        var request = new CustomerAddressRequest(
            "İş Adresi", "İstanbul", "Ümraniye", "Finanskent", "Büyükdere Sokak",
            null, "12", null, null, "34760");

        await service.AddAddressAsync(customer.Id, request, default);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AddAddressAsync(customer.Id, request with { Street = "İkinci Sokak" }, default));

        Assert.Contains("zaten kayıtlıdır", exception.Message);
    }

    [Fact]
    public async Task Create_BlocksOpenDuplicateEvenWhenAcknowledged()
    {
        var service = CreateService(out var repository);
        repository.RecentSameCardType = CreatePendingApplication();
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new CreateCardApplicationRequest(10, 1, 20_000m), 1, default));
        Assert.Contains("hâlen değerlendirmededir", exception.Message);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(new CreateCardApplicationRequest(
            10, 1, 20_000m, DuplicateWarningAcknowledged: true), 1, default));
    }

    [Fact]
    public async Task Create_RejectsInvalidStatementDay()
    {
        var service = CreateService(out _);
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(new CreateCardApplicationRequest(10, 1, 20_000m, StatementDay: 29), 1, default));
        Assert.Contains("7, 14, 21 veya 28", exception.Message);
    }

    [Fact]
    public async Task Create_RejectsMissingRequiredDocuments()
    {
        var service = CreateService(out _);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new CreateCardApplicationRequest(
                10, 1, 20_000m,
                IdentityDocumentConfirmed: true,
                IncomeDocumentConfirmed: false,
                ResidenceDocumentConfirmed: false), 1, default));

        Assert.Contains("Gelir Belgesi", exception.Message);
        Assert.Contains("İkametgah", exception.Message);
    }

    [Fact]
    public async Task Revision_PersistsBeforeAndAfterSnapshots()
    {
        var service = CreateService(out var repository);
        repository.TrackedApplication = CreatePendingApplication();
        await service.EvaluateAsync(1, new EvaluateCardApplicationRequest(
            "Revision", null, "Limit yeniden düzenlenmeli."), 2, default);
        await service.ResubmitAsync(1, new ResubmitCardApplicationRequest(
            1, 15_000m, StatementDay: 21, AutomaticLimitIncreaseEnabled: true,
            IdentityDocumentConfirmed: true, IncomeDocumentConfirmed: true,
            ResidenceDocumentConfirmed: true), 1, default);

        Assert.Equal(2, repository.TrackedApplication.RevisionSnapshots.Count);
        Assert.Contains(repository.TrackedApplication.RevisionSnapshots, x => x.Stage == "Before");
        Assert.Contains(repository.TrackedApplication.RevisionSnapshots, x => x.Stage == "After");
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(4, 1)]
    [InlineData(6, 2)]
    [InlineData(8, 3)]
    [InlineData(12, 4)]
    [InlineData(24, 5)]
    public void Escalation_UsesConfiguredSlaThresholds(double hours, int expected) =>
        Assert.Equal(expected, ApplicationOperationsPolicy.DetermineEscalationLevel(hours));

    [Fact]
    public void Cancellation_SeparatesOfficerWithdrawalFromManagerCancellation()
    {
        Assert.Equal(ApplicationStatus.Withdrawn,
            ApplicationOperationsPolicy.DetermineCancellationStatus(
                ApplicationStatus.Pending, false, false, "Müşteri başvurusunu geri çekti."));
        Assert.Equal(ApplicationStatus.Cancelled,
            ApplicationOperationsPolicy.DetermineCancellationStatus(
                ApplicationStatus.Revision, false, true, "Operasyonel mükerrer kayıt tespit edildi."));
    }

    [Fact]
    public void Cancellation_BlocksApprovedOrIssuedApplication() =>
        Assert.Throws<InvalidOperationException>(() => ApplicationOperationsPolicy.DetermineCancellationStatus(
            ApplicationStatus.Approved, true, true, "Kart üretildiği için iptal deneniyor."));

    [Fact]
    public void Reassignment_RequiresAuditableReason() =>
        Assert.Throws<ArgumentException>(() => ApplicationOperationsPolicy.ValidateReassignmentReason("kısa"));

    [Fact]
    public void SlaClock_IgnoresAssignmentAndEscalationHistoryEntries()
    {
        var createdAt = DateTime.UtcNow.AddHours(-10);
        var application = CreatePendingApplication();
        application.CreatedAtUtc = createdAt;
        application.Histories.Add(new ApplicationHistory
        {
            PreviousStatus = ApplicationStatus.Pending,
            NewStatus = ApplicationStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            Description = "SLA yükseltmesi veya iş ataması"
        });

        Assert.Equal(createdAt, ApplicationOperationsPolicy.GetCurrentStageStartedAt(application));
    }

    [Fact]
    public void SlaClock_StartsAtLatestRealStatusTransition()
    {
        var revisionStartedAt = DateTime.UtcNow.AddHours(-3);
        var application = CreatePendingApplication();
        application.Status = ApplicationStatus.Revision;
        application.Histories.Add(new ApplicationHistory
        {
            PreviousStatus = ApplicationStatus.Pending,
            NewStatus = ApplicationStatus.Revision,
            CreatedAtUtc = revisionStartedAt,
            Description = "Revizyon istendi"
        });
        application.Histories.Add(new ApplicationHistory
        {
            PreviousStatus = ApplicationStatus.Revision,
            NewStatus = ApplicationStatus.Revision,
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-10),
            Description = "SLA yükseltmesi"
        });

        Assert.Equal(revisionStartedAt, ApplicationOperationsPolicy.GetCurrentStageStartedAt(application));
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

    private static CreateCustomerRequest ValidCustomerRequest() => new(
        "10000000078", "Test", "Müşteri", "+90", "5551112233", "customer@example.com",
        30_000m, 0m, new DateOnly(1990, 1, 1), "Kadın", "Lisans", "Mühendis",
        "Çalışıyor", "İstanbul", "Kadıköy", "Caferağa", "Caferağa Mahallesi Test Sokak No: 1")
    {
        Street = "Test Sokak", BuildingNo = "1", PostalCode = "34710"
    };
}

internal sealed class FakeCustomerRepository : ICustomerRepository
{
    public bool DuplicatePhone { get; set; }
    public bool DuplicateEmail { get; set; }
    private Customer? _customer;
    public Task<Customer?> GetByIdAsync(int id, CancellationToken cancellationToken) => Task.FromResult(_customer);
    public Task<Customer?> GetDetailByIdAsync(int id, CancellationToken cancellationToken) => Task.FromResult(_customer);
    public Task<Customer?> GetTrackedByIdAsync(int id, CancellationToken cancellationToken) => Task.FromResult(_customer);
    public Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Customer>>(_customer is null ? [] : [_customer]);
    public Task<IReadOnlyList<Customer>> SearchAsync(string query, string? criterion, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Customer>>(_customer is null ? [] : [_customer]);
    public Task<bool> NationalIdentityNumberExistsAsync(string nationalIdentityNumber, CancellationToken cancellationToken) =>
        Task.FromResult(false);
    public Task<bool> PhoneNumberExistsAsync(string countryCode, string phoneNumber, int? excludedCustomerId, CancellationToken cancellationToken) =>
        Task.FromResult(DuplicatePhone);
    public Task<bool> EmailAddressExistsAsync(string emailAddress, int? excludedCustomerId, CancellationToken cancellationToken) =>
        Task.FromResult(DuplicateEmail);
    public Task<bool> HasOpenApplicationsAsync(int customerId, CancellationToken cancellationToken) => Task.FromResult(false);
    public Task<int> GetNextCustomerSequenceAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    public Task AddAsync(Customer customer, CancellationToken cancellationToken)
    {
        customer.Id = 1;
        var cardId = 1;
        foreach (var card in customer.OtherBankCards) card.Id = cardId++;
        var addressId = 1;
        foreach (var address in customer.SavedAddresses) address.Id = addressId++;
        _customer = customer;
        return Task.CompletedTask;
    }
    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        if (_customer is not null)
        {
            var nextId = _customer.SavedAddresses.Where(x => x.Id > 0).Select(x => x.Id).DefaultIfEmpty().Max() + 1;
            foreach (var address in _customer.SavedAddresses.Where(x => x.Id == 0)) address.Id = nextId++;
        }
        return Task.CompletedTask;
    }
}

internal sealed class FakeCardApplicationRepository : ICardApplicationRepository
{
    public bool HasOpenApplication { get; set; }
    public CardApplication? RecentSameCardType { get; set; }
    public CardApplication? TrackedApplication { get; set; }
    public bool HasKvkkConsent { get; set; } = true;
    public bool HasRequiredDocuments { get; set; } = true;

    public static Customer CreateCustomer() => new()
    {
        Id = 10, CustomerNumber = "MUS000001", NationalIdentityNumber = "10000000078",
        FirstName = "Test", LastName = "Müşteri", PhoneCountryCode = "+90",
        PhoneNumber = "5555555555", IsPhoneVerified = true,
        EmailAddress = "test@example.com", IsEmailVerified = true, MonthlyNetIncome = 20_000m,
        OtherBankTotalCardLimit = 10_000m, IsActive = true,
        BirthDate = new DateOnly(1990, 1, 1), Gender = "Kadın", EducationLevel = "Lisans",
        Occupation = "Mühendis", City = "İstanbul", District = "Kadıköy",
        Neighborhood = "Caferağa", Address = "Caferağa Mahallesi Kadıköy İstanbul"
    };

    public static CardType CreateCardType() => new()
    {
        Id = 1, Name = "Classic", Bin = "45000101", MinimumLimit = 5_000m,
        MaximumLimit = 50_000m, IsActive = true
    };

    public Task<Customer?> GetCustomerAsync(int customerId, CancellationToken cancellationToken) => Task.FromResult<Customer?>(CreateCustomer());
    public Task<CardType?> GetCardTypeAsync(int cardTypeId, CancellationToken cancellationToken) => Task.FromResult<CardType?>(CreateCardType());
    public Task<IReadOnlyList<CardType>> GetActiveCardTypesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<CardType>>([CreateCardType()]);
    public Task<bool> HasApprovedCardTypeAsync(int customerId, int cardTypeId, CancellationToken cancellationToken) => Task.FromResult(false);
    public Task<CardApplication?> GetRecentSameCardTypeAsync(int customerId, int cardTypeId, DateTime thresholdUtc, CancellationToken cancellationToken) =>
        Task.FromResult(RecentSameCardType);
    public Task<decimal> GetApprovedCardLimitTotalAsync(int customerId, CancellationToken cancellationToken) => Task.FromResult(0m);
    public Task<bool> HasCurrentGrantedConsentAsync(int customerId, string consentType, CancellationToken cancellationToken) =>
        Task.FromResult(HasKvkkConsent);
    public Task<bool> HasRequiredDocumentsAsync(int applicationId, CancellationToken cancellationToken) =>
        Task.FromResult(HasRequiredDocuments);
    public Task<bool> HasEarlierOpenApplicationAsync(int customerId, int applicationId, DateTime createdAtUtc, CancellationToken cancellationToken) => Task.FromResult(false);
    public Task<CardApplication?> GetByIdAsync(int id, CancellationToken cancellationToken) => Task.FromResult(TrackedApplication);
    public Task<bool> IsCreatedByAsync(int applicationId, int userId, CancellationToken cancellationToken) => Task.FromResult(TrackedApplication?.CreatedByUserId == userId);
    public Task<IReadOnlyList<CardApplication>> GetByOfficerAsync(int userId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<CardApplication>>([]);
    public Task<IReadOnlyList<CardApplication>> GetPendingAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<CardApplication>>([]);
    public Task<IReadOnlyList<CardApplication>> GetAllAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<CardApplication>>([]);
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
