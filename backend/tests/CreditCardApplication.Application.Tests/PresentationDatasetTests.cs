using CreditCardApplication.Domain.Enums;
using CreditCardApplication.Application.Services;
using CreditCardApplication.Infrastructure.Platform;
using CreditCardApplication.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CreditCardApplication.Application.Tests;

public sealed class PresentationDatasetTests(PresentationDatasetFixture fixture)
    : IClassFixture<PresentationDatasetFixture>
{
    [Fact]
    public async Task Dataset_HasExactRequestedPopulation()
    {
        await using var db = fixture.CreateContext();
        Assert.Equal(200, await db.Customers.CountAsync());
        Assert.Equal(50, await db.UserRoles.CountAsync(x => x.RoleId == 1));
        Assert.Equal(5, await db.UserRoles.CountAsync(x => x.RoleId == 2));
    }

    [Fact]
    public async Task CustomerIdentityAndContactData_AreValidAndUnique()
    {
        await using var db = fixture.CreateContext();
        var customers = await db.Customers.AsNoTracking().ToListAsync();
        Assert.All(customers, customer => Assert.True(
            PresentationDataSeeder.IsValidTurkishIdentityNumber(customer.NationalIdentityNumber),
            $"Geçersiz TC: {customer.NationalIdentityNumber}"));
        Assert.Equal(customers.Count, customers.Select(x => x.NationalIdentityNumber).Distinct().Count());
        Assert.Equal(customers.Count, customers.Select(x => $"{x.PhoneCountryCode}{x.PhoneNumber}").Distinct().Count());
        Assert.Equal(customers.Count, customers.Select(x => x.EmailAddress).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public async Task EveryCustomer_HasOneApprovedMainCard()
    {
        await using var db = fixture.CreateContext();
        var counts = await db.Customers.AsNoTracking()
            .Select(customer => customer.Applications.Count(application =>
                application.Status == ApplicationStatus.Approved && application.CreditCard != null))
            .ToListAsync();
        Assert.Equal(200, counts.Count);
        Assert.All(counts, count => Assert.Equal(1, count));
    }

    [Fact]
    public async Task EveryMainCard_HasOneApprovedSupplementaryCard()
    {
        await using var db = fixture.CreateContext();
        var mainCardIds = await db.CreditCards.AsNoTracking().Select(x => x.Id).ToListAsync();
        var supplementary = await db.SupplementaryCardApplications.AsNoTracking().ToListAsync();
        Assert.Equal(200, mainCardIds.Count);
        Assert.Equal(200, supplementary.Count);
        Assert.All(mainCardIds, cardId => Assert.Single(supplementary, x => x.PrimaryCreditCardId == cardId));
        Assert.All(supplementary, item => Assert.Equal("Approved", item.Status));
    }

    [Fact]
    public async Task SupplementaryCardHolder_IsNeverPrimaryCustomer()
    {
        await using var db = fixture.CreateContext();
        var supplementary = await db.SupplementaryCardApplications.AsNoTracking().ToListAsync();
        Assert.All(supplementary, item => Assert.NotEqual(item.PrimaryCustomerId, item.SupplementaryHolderCustomerId));
        Assert.Equal(200, supplementary.Select(x => x.SupplementaryHolderCustomerId).Distinct().Count());
    }

    [Fact]
    public async Task PersistedCardMasks_AreUniqueAndMatchConfiguredBins()
    {
        await using var db = fixture.CreateContext();
        var mainCards = await db.CreditCards.AsNoTracking()
            .Include(x => x.CardApplication).ThenInclude(x => x.CardType).ToListAsync();
        var supplementary = await db.SupplementaryCardApplications.AsNoTracking()
            .Include(x => x.PrimaryCreditCard).ThenInclude(x => x.CardApplication).ThenInclude(x => x.CardType)
            .ToListAsync();
        Assert.All(mainCards, card => Assert.StartsWith(card.CardApplication.CardType.Bin, card.MaskedCardNumber));
        Assert.All(supplementary, card => Assert.StartsWith(
            card.PrimaryCreditCard.CardApplication.CardType.Bin, card.MaskedCardNumber));
        var masks = mainCards.Select(x => x.MaskedCardNumber)
            .Concat(supplementary.Select(x => x.MaskedCardNumber!)).ToList();
        Assert.Equal(400, masks.Distinct().Count());
        Assert.All(masks, mask => Assert.Contains('*', mask));
    }

    [Fact]
    public async Task Dataset_CoversApplicationFulfillmentAndLimitScenarios()
    {
        await using var db = fixture.CreateContext();
        var statuses = await db.CardApplications.AsNoTracking().GroupBy(x => x.Status)
            .Select(group => new { group.Key, Count = group.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
        Assert.True(statuses[ApplicationStatus.Approved] >= 200);
        Assert.True(statuses[ApplicationStatus.Pending] > 0);
        Assert.True(statuses[ApplicationStatus.Revision] > 0);
        Assert.True(statuses[ApplicationStatus.Rejected] > 0);
        Assert.True(statuses[ApplicationStatus.Cancelled] > 0);
        Assert.True(await db.CardFulfillments.AnyAsync(x => x.Status == "Delivered"));
        Assert.True(await db.CardFulfillments.AnyAsync(x => x.Status == "Printing"));
        Assert.True(await db.CardFulfillments.AnyAsync(x => x.Status == "Production"));
        Assert.True(await db.CreditCards.AnyAsync(x => x.LimitIncreaseStatus == "Pending"));
        Assert.True(await db.CreditCards.AnyAsync(x => x.LimitIncreaseStatus == "Approved"));
        Assert.True(await db.CreditCards.AnyAsync(x => x.LimitIncreaseStatus == "Rejected"));
    }

    [Fact]
    public async Task OtherBankLimit_EqualsActiveOtherBankCardSum()
    {
        await using var db = fixture.CreateContext();
        var customers = await db.Customers.AsNoTracking().Include(x => x.OtherBankCards).ToListAsync();
        Assert.All(customers, customer => Assert.Equal(customer.OtherBankTotalCardLimit,
            customer.OtherBankCards.Where(x => x.IsActive).Sum(x => x.CardLimit)));
    }

    [Fact]
    public async Task Dataset_SpansMultipleMonthsAndYearsForDashboardAnalysis()
    {
        await using var db = fixture.CreateContext();
        var dates = await db.CardApplications.AsNoTracking().Select(x => x.CreatedAtUtc).ToListAsync();
        Assert.True(dates.Select(x => x.Year).Distinct().Count() >= 2);
        Assert.True(dates.Select(x => new { x.Year, x.Month }).Distinct().Count() >= 12);
    }

    [Fact]
    public async Task DuplicateCheck_FindsApprovedCardOutsideFifteenDayWindow()
    {
        await using var db = fixture.CreateContext();
        var approved = await db.CardApplications.AsNoTracking()
            .Where(x => x.Status == ApplicationStatus.Approved && x.CreditCard != null
                && x.CreatedAtUtc < DateTime.UtcNow.AddDays(-15))
            .OrderBy(x => x.CreatedAtUtc)
            .FirstAsync();
        var service = new PlatformV2Service(db, new ConfigurationBuilder().Build(), new LimitCalculator());

        var result = await service.CheckDuplicateAsync(
            approved.CustomerId, approved.CardTypeId, CancellationToken.None);

        Assert.True(result.HasDuplicate);
        Assert.Equal(approved.Id, result.ApplicationId);
        Assert.Equal(ApplicationStatus.Approved.ToString(), result.Status);
    }

    [Fact]
    public async Task SupplementaryContext_IsAvailableToAnotherAuthorizedOfficer()
    {
        await using var db = fixture.CreateContext();
        var card = await db.CreditCards.AsNoTracking()
            .Include(x => x.CardApplication)
            .FirstAsync(x => x.Status == CardStatus.Active);
        var anotherOfficerId = await db.UserRoles.AsNoTracking()
            .Where(x => x.RoleId == 1 && x.UserId != card.CardApplication.CreatedByUserId)
            .Select(x => x.UserId)
            .FirstAsync();
        var service = new PlatformV2Service(db, new ConfigurationBuilder().Build(), new LimitCalculator());

        var context = await service.GetSupplementaryCardContextAsync(
            card.Id, anotherOfficerId, CancellationToken.None);

        Assert.Equal(card.Id, context.PrimaryCreditCardId);
        Assert.Equal(CardStatus.Active.ToString(), context.PrimaryCardStatus);
    }
}

public sealed class PresentationDatasetFixture : IAsyncLifetime
{
    private readonly string databasePath = Path.Combine(Path.GetTempPath(), $"kbys-presentation-tests-{Guid.NewGuid():N}.db");

    public ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"Data Source={databasePath};Foreign Keys=True")
            .Options;
        return new ApplicationDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DemoData:OfficerPassword"] = "DemoLogin01!",
            ["DemoData:ManagerPassword"] = "DemoLogin01!",
            ["DemoData:ScenarioOfficerPassword"] = "DemoLogin01!",
            ["DemoData:ScenarioManagerPassword"] = "DemoLogin01!",
            ["DemoData:BranchOfficerPassword"] = "DemoLogin01!"
        }).Build();
        await PresentationDataSeeder.EnsureCardCatalogAsync(db);
        await PresentationDataSeeder.EnsureStaffAsync(db, configuration);
        await PresentationDataSeeder.SeedDatasetAsync(db);
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(databasePath)) File.Delete(databasePath);
        return Task.CompletedTask;
    }
}
