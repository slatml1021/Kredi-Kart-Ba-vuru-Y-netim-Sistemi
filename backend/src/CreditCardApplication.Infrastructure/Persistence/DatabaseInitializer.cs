using CreditCardApplication.Domain.Entities;
using CreditCardApplication.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CreditCardApplication.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static async Task InitializeDatabaseAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);

        var demoUsers = await dbContext.Users
            .Where(x => x.PasswordHash == "DEMO_ONLY")
            .ToListAsync(cancellationToken);
        foreach (var user in demoUsers)
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("Demo123!");
        if (demoUsers.Count > 0)
            await dbContext.SaveChangesAsync(cancellationToken);

        if (!await dbContext.Customers.AnyAsync(x => x.CustomerNumber == "CUS-DEMO-0001", cancellationToken))
            await SeedDemoWorkflowAsync(dbContext, cancellationToken);
    }

    private static async Task SeedDemoWorkflowAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var customers = new[]
        {
            CreateCustomer("CUS-DEMO-0001", "10000000001", "Elif", "Erdem", "05550000001", "elif.erdem@example.com", 30_000m, 10_000m),
            CreateCustomer("CUS-DEMO-0002", "10000000002", "Fatma", "Fidan", "05550000002", "fatma.fidan@example.com", 50_000m, 20_000m),
            CreateCustomer("CUS-DEMO-0003", "10000000003", "Gökhan", "Güler", "05550000003", "gokhan.guler@example.com", 20_000m, 10_000m),
            CreateCustomer("CUS-DEMO-0004", "10000000004", "Hakan", "Hızır", "05550000004", "hakan.hizir@example.com", 40_000m, 20_000m)
        };
        dbContext.Customers.AddRange(customers);
        await dbContext.SaveChangesAsync(cancellationToken);

        var pending = CreateApplication("KKB-DEMO-0001", customers[0].Id, 1, 20_000m, ApplicationStatus.Pending, now.AddHours(-2));
        AddHistory(pending, null, ApplicationStatus.Pending, 1, "Başvuru memur tarafından oluşturuldu.", now.AddHours(-2));

        var approved = CreateApplication("KKB-DEMO-0002", customers[1].Id, 2, 60_000m, ApplicationStatus.Approved, now.AddDays(-1));
        approved.ApprovedLimit = 60_000m;
        approved.EvaluatedByUserId = 2;
        approved.EvaluatedAtUtc = now.AddMinutes(-45);
        approved.EvaluationNote = "Gelir ve limit kriterleri uygundur.";
        approved.CreditCard = new CreditCard
        {
            MaskedCardNumber = "45000102 **** 2026", CardLimit = 60_000m, Status = CardStatus.Inactive,
            IssueDate = DateOnly.FromDateTime(now), ExpiryDate = DateOnly.FromDateTime(now.AddYears(4))
        };
        AddHistory(approved, null, ApplicationStatus.Pending, 1, "Başvuru memur tarafından oluşturuldu.", now.AddDays(-1));
        AddHistory(approved, ApplicationStatus.Pending, ApplicationStatus.Approved, 2, approved.EvaluationNote, now.AddMinutes(-45));

        var rejected = CreateApplication("KKB-DEMO-0003", customers[2].Id, 1, 15_000m, ApplicationStatus.Rejected, now.AddDays(-2));
        rejected.EvaluatedByUserId = 2;
        rejected.EvaluatedAtUtc = now.AddHours(-3);
        rejected.EvaluationNote = "Başvuru mevcut değerlendirme kriterlerini karşılamadı.";
        AddHistory(rejected, null, ApplicationStatus.Pending, 1, "Başvuru memur tarafından oluşturuldu.", now.AddDays(-2));
        AddHistory(rejected, ApplicationStatus.Pending, ApplicationStatus.Rejected, 2, rejected.EvaluationNote, now.AddHours(-3));

        var revision = CreateApplication("KKB-DEMO-0004", customers[3].Id, 2, 70_000m, ApplicationStatus.Revision, now.AddHours(-5));
        revision.EvaluatedByUserId = 2;
        revision.EvaluatedAtUtc = now.AddHours(-1);
        revision.EvaluationNote = "Talep edilen limit ve müşteri gelir bilgisi yeniden kontrol edilmelidir.";
        AddHistory(revision, null, ApplicationStatus.Pending, 1, "Başvuru memur tarafından oluşturuldu.", now.AddHours(-5));
        AddHistory(revision, ApplicationStatus.Pending, ApplicationStatus.Revision, 2, revision.EvaluationNote, now.AddHours(-1));

        dbContext.CardApplications.AddRange(pending, approved, rejected, revision);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static Customer CreateCustomer(string number, string identity, string firstName, string lastName,
        string phone, string email, decimal income, decimal otherBankLimit) => new()
    {
        CustomerNumber = number, NationalIdentityNumber = identity, FirstName = firstName, LastName = lastName,
        PhoneNumber = phone, EmailAddress = email, MonthlyNetIncome = income,
        OtherBankTotalCardLimit = otherBankLimit, IsActive = true
    };

    private static CardApplication CreateApplication(string number, int customerId, int cardTypeId,
        decimal requestedLimit, ApplicationStatus status, DateTime createdAtUtc) => new()
    {
        ApplicationNumber = number, CustomerId = customerId, CardTypeId = cardTypeId, CreatedByUserId = 1,
        RequestedLimit = requestedLimit, Status = status, CreatedAtUtc = createdAtUtc
    };

    private static void AddHistory(CardApplication application, ApplicationStatus? previousStatus,
        ApplicationStatus newStatus, int userId, string? description, DateTime createdAtUtc) =>
        application.Histories.Add(new ApplicationHistory
        {
            PreviousStatus = previousStatus, NewStatus = newStatus, ChangedByUserId = userId,
            Description = description, CreatedAtUtc = createdAtUtc
        });
}
