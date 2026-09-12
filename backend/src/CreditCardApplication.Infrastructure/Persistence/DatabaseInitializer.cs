using System.Data;
using CreditCardApplication.Domain.Entities;
using CreditCardApplication.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
#pragma warning disable EF1002 // Sütun adları yalnızca bu dosyadaki sabit sözlüklerden gelir.

namespace CreditCardApplication.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static async Task InitializeDatabaseAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();

        await EnsureSqliteHealthAsync(dbContext, cancellationToken);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "UPDATE Users SET RegistrationNumber = 'KBP' || substr(RegistrationNumber, 4) WHERE RegistrationNumber LIKE 'BSP______';",
            cancellationToken);
        await EnsureCustomerSchemaAsync(dbContext, cancellationToken);
        await EnsureCustomerAddressSchemaAsync(dbContext, cancellationToken);
        await EnsureOtherBankCardSchemaAsync(dbContext, cancellationToken);
        await EnsureApplicationSchemaAsync(dbContext, cancellationToken);
        await EnsureCreditCardSchemaAsync(dbContext, cancellationToken);
        await EnsurePlatformV2SchemaAsync(dbContext, cancellationToken);
        await PresentationDataSeeder.EnsureCardCatalogAsync(dbContext, cancellationToken);
        await EnsureCustomerNumberFormatAsync(dbContext, cancellationToken);

        var demoDataEnabled = bool.TryParse(configuration["DemoData:Enabled"], out var enabled) && enabled;
        if (!environment.IsDevelopment() || !demoDataEnabled)
            return;

        await PresentationDataSeeder.EnsureStaffAsync(dbContext, configuration, cancellationToken);

        if (!await dbContext.Customers.AnyAsync(cancellationToken))
            await PresentationDataSeeder.SeedDatasetAsync(dbContext, cancellationToken);
        await EnsureEceAydinDemoAsync(dbContext, cancellationToken);
        await EnsureDemoCustomerConsentsAsync(dbContext, cancellationToken);
        await EnsureCustomerPortalAccountsAsync(dbContext, configuration, cancellationToken);
        await EnsureCustomerDemoDetailsAsync(dbContext, cancellationToken);
        await EnsureRevisionDemoAsync(dbContext, cancellationToken);
        await EnsureDemoOperationScenariosAsync(dbContext, environment.ContentRootPath, cancellationToken);
    }

    private static async Task EnsureCustomerNumberFormatAsync(
        ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        var customers = await dbContext.Customers.OrderBy(x => x.Id).ToListAsync(cancellationToken);
        var used = customers
            .Where(x => System.Text.RegularExpressions.Regex.IsMatch(x.CustomerNumber, "^MUS[0-9]{6}$"))
            .Select(x => x.CustomerNumber).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sequence = Math.Max(1, customers.Count == 0 ? 1 : customers.Max(x => x.Id));
        foreach (var customer in customers.Where(x =>
                     !System.Text.RegularExpressions.Regex.IsMatch(x.CustomerNumber, "^MUS[0-9]{6}$")))
        {
            string normalized;
            do { normalized = $"MUS{sequence++:D6}"; } while (used.Contains(normalized));
            var account = await dbContext.CustomerAccounts.FirstOrDefaultAsync(
                x => x.CustomerId == customer.Id, cancellationToken);
            customer.CustomerNumber = normalized;
            if (account is not null) account.Username = normalized;
            used.Add(normalized);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureDemoCustomerConsentsAsync(
        ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        var officerId = await dbContext.Users.AsNoTracking()
            .Where(x => x.UserRoles.Any(role => role.Role.Name == "Officer"))
            .Select(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (officerId == 0) return;
        var customerIds = await dbContext.Customers.AsNoTracking()
            .Where(x => x.CustomerNumber.StartsWith("MUS") || x.CustomerNumber.StartsWith("CUS-DEMO"))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        foreach (var customerId in customerIds)
        foreach (var type in new[] { "KVKK", "EMAIL", "SMS" })
        {
            if (await dbContext.CustomerConsents.AnyAsync(
                    x => x.CustomerId == customerId && x.ConsentType == type, cancellationToken))
                continue;
            dbContext.CustomerConsents.Add(new CustomerConsent
            {
                CustomerId = customerId,
                ConsentType = type,
                TextVersion = "v1.0-demo",
                IsGranted = true,
                Channel = "Şube",
                CapturedByUserId = officerId,
                CapturedAtUtc = DateTime.UtcNow
            });
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedDemoWorkflowAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var customers = new[]
        {
            CreateCustomer("MUS000001", "10000000078", "Elif", "Erdem", "05550000001", "elif.erdem@example.com", 30_000m, 10_000m),
            CreateCustomer("MUS000002", "10000000146", "Fatma", "Fidan", "05550000002", "fatma.fidan@example.com", 50_000m, 20_000m),
            CreateCustomer("MUS000003", "10000000214", "Gökhan", "Güler", "05550000003", "gokhan.guler@example.com", 20_000m, 10_000m),
            CreateCustomer("MUS000004", "10000000382", "Hakan", "Hızır", "05550000004", "hakan.hizir@example.com", 40_000m, 20_000m)
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

    private static async Task EnsureCardProductCatalogAsync(
        ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        var definitions = new[]
        {
            new { Id = 1, Name = "Classic Visa", Bin = "45000101", Min = 5_000m, Max = 50_000m },
            new { Id = 2, Name = "Gold Mastercard", Bin = "54000102", Min = 15_000m, Max = 150_000m },
            new { Id = 3, Name = "Platinum Visa", Bin = "45000103", Min = 30_000m, Max = 300_000m },
            new { Id = 4, Name = "Platinum Plus Visa", Bin = "45000104", Min = 50_000m, Max = 500_000m },
            new { Id = 5, Name = "Classic TROY", Bin = "97920101", Min = 5_000m, Max = 50_000m },
            new { Id = 6, Name = "Gold TROY", Bin = "97920102", Min = 15_000m, Max = 150_000m },
            new { Id = 7, Name = "Classic Mastercard", Bin = "54000101", Min = 5_000m, Max = 50_000m },
            new { Id = 8, Name = "Gold Visa", Bin = "45000102", Min = 15_000m, Max = 150_000m },
            new { Id = 9, Name = "Platinum Mastercard", Bin = "54000103", Min = 30_000m, Max = 300_000m },
            new { Id = 10, Name = "Platinum TROY", Bin = "97920103", Min = 30_000m, Max = 300_000m },
            new { Id = 11, Name = "Platinum Plus Mastercard", Bin = "54000104", Min = 50_000m, Max = 500_000m },
            new { Id = 12, Name = "Platinum Plus TROY", Bin = "97920104", Min = 50_000m, Max = 500_000m },
        };
        foreach (var definition in definitions)
        {
            var cardType = await dbContext.CardTypes.FirstOrDefaultAsync(x => x.Id == definition.Id, cancellationToken);
            if (cardType is null)
            {
                dbContext.CardTypes.Add(new CardType
                {
                    Id = definition.Id, Name = definition.Name, Bin = definition.Bin,
                    MinimumLimit = definition.Min, MaximumLimit = definition.Max, IsActive = true
                });
                continue;
            }
            cardType.Name = definition.Name;
            cardType.Bin = definition.Bin;
            cardType.MinimumLimit = definition.Min;
            cardType.MaximumLimit = definition.Max;
            cardType.IsActive = true;
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureScenarioUsersAsync(
        ApplicationDbContext dbContext,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var definitions = new[]
        {
            new { Id = 1, Registration = "KBP000001", Name = "Sıla Temel", PasswordKey = "OfficerPassword", RoleId = 1, Email = "sila.temel@kartbasvuru.local", Phone = "+905551234567", Title = "Kart Başvuru Memuru", Branch = "Finanskent Şubesi" },
            new { Id = 2, Registration = "KBP000002", Name = "Ayşe Yılmaz", PasswordKey = "ManagerPassword", RoleId = 2, Email = "ayse.yilmaz@kartbasvuru.local", Phone = "+905559876543", Title = "Kart Başvuru Müdürü", Branch = "Genel Müdürlük" },
            new { Id = 3, Registration = "KBP000003", Name = "Mert Kaya", PasswordKey = "ScenarioOfficerPassword", RoleId = 1, Email = "kbp000003@kartbasvuru.local", Phone = "+90555000003", Title = "Kart Başvuru Memuru", Branch = "Ataşehir Şubesi" },
            new { Id = 4, Registration = "KBP000004", Name = "Deniz Arslan", PasswordKey = "ScenarioManagerPassword", RoleId = 2, Email = "kbp000004@kartbasvuru.local", Phone = "+90555000004", Title = "Kart Başvuru Müdürü", Branch = "Genel Müdürlük" },
            new { Id = 5, Registration = "KBP000005", Name = "Ece Şahin", PasswordKey = "BranchOfficerPassword", RoleId = 1, Email = "kbp000005@kartbasvuru.local", Phone = "+90555000005", Title = "Şube Operasyon Memuru", Branch = "Kadıköy Şubesi" },
        };
        foreach (var definition in definitions)
        {
            var configuredPassword = GetRequiredDemoPassword(configuration, definition.PasswordKey);
            var user = await dbContext.Users
                .Include(x => x.UserRoles)
                .SingleOrDefaultAsync(x => x.RegistrationNumber == definition.Registration, cancellationToken);
            if (user is null)
            {
                user = new User
                {
                    Id = definition.Id,
                    RegistrationNumber = definition.Registration,
                    FullName = definition.Name,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(configuredPassword),
                    CorporateEmail = definition.Email,
                    PhoneNumber = definition.Phone,
                    Title = definition.Title,
                    Department = "Kart Başvuru Operasyonları",
                    Branch = definition.Branch,
                    PasswordChangedAtUtc = DateTime.UtcNow,
                    UserRoles = [new UserRole { RoleId = definition.RoleId }]
                };
                dbContext.Users.Add(user);
                continue;
            }

            var passwordMatches = user.PasswordHash.StartsWith("$2", StringComparison.Ordinal)
                && BCrypt.Net.BCrypt.Verify(configuredPassword, user.PasswordHash);
            if (!passwordMatches)
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(configuredPassword);
                user.PasswordChangedAtUtc = DateTime.UtcNow;
            }
            if (user.UserRoles.All(x => x.RoleId != definition.RoleId))
                user.UserRoles.Add(new UserRole { RoleId = definition.RoleId });
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string GetRequiredDemoPassword(IConfiguration configuration, string key) =>
        configuration[$"DemoData:{key}"] is { Length: >= 10 } password
            ? password
            : throw new InvalidOperationException($"DemoData:{key} en az 10 karakter olacak şekilde yapılandırılmalıdır.");

    private static Customer CreateCustomer(string number, string identity, string firstName, string lastName,
        string phone, string email, decimal income, decimal otherBankLimit) => new()
    {
        CustomerNumber = number, NationalIdentityNumber = identity, FirstName = firstName, LastName = lastName,
        PhoneCountryCode = "+90", PhoneNumber = phone.StartsWith('0') ? phone[1..] : phone,
        IsPhoneVerified = true, EmailAddress = email, IsEmailVerified = true, MonthlyNetIncome = income,
        OtherBankTotalCardLimit = otherBankLimit, IsActive = true,
        BirthDate = new DateOnly(1992, 5, 12),
        Gender = firstName is "Elif" or "Fatma" ? "Kadın" : "Erkek",
        EducationLevel = "Lisans",
        Occupation = "Mühendis", City = "İstanbul", District = "Ümraniye",
        Neighborhood = "Finanskent Mahallesi",
        Address = "Finanskent Mahallesi, Örnek Sokak No: 1, Ümraniye / İstanbul",
        CreditScore = 1650,
        SavedAddresses =
        [
            new CustomerAddress
            {
                Name = "Ev Adresi", City = "İstanbul", District = "Ümraniye",
                Neighborhood = "Finanskent Mahallesi", Street = "Örnek Sokak",
                BuildingNo = "1", PostalCode = "34760",
                FullAddress = "Finanskent Mahallesi, Örnek Sokak, Bina No: 1, 34760, Ümraniye / İstanbul",
                IsDefault = true
            }
        ],
        OtherBankCards = otherBankLimit > 0
            ? [new OtherBankCard { BankName = "Beyan Edilen Toplam", CardLimit = otherBankLimit, IsActive = true }]
            : []
    };

    private static async Task EnsureEceAydinDemoAsync(
        ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        var customer = await dbContext.Customers
            .Include(x => x.Applications).ThenInclude(x => x.CreditCard)
            .FirstOrDefaultAsync(x => x.NationalIdentityNumber == "10000000450", cancellationToken);
        if (customer is null)
        {
            customer = CreateCustomer("MUS000005", "10000000450", "Ece", "Aydın",
                "05550000006", "ece.aydin@example.com", 55_000m, 15_000m);
            dbContext.Customers.Add(customer);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (customer.Applications.Any(x => x.CreditCard is not null)) return;
        var application = CreateApplication("KKB-DEMO-ECE-01", customer.Id, 8, 75_000m,
            ApplicationStatus.Approved, DateTime.UtcNow.AddDays(-3));
        application.ApprovedLimit = 75_000m;
        application.AssignedOfficerUserId = 1;
        application.EvaluatedByUserId = 2;
        application.EvaluatedAtUtc = DateTime.UtcNow.AddDays(-2);
        application.EvaluationNote = "Sunum senaryosu: gelir, iletişim doğrulaması ve limit koşulları uygundur.";
        application.CreditCard = new CreditCard
        {
            MaskedCardNumber = "45000102 **** 0826", CardLimit = 75_000m,
            Status = CardStatus.Active, IssueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)),
            ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(4))
        };
        AddHistory(application, null, ApplicationStatus.Pending, 1,
            "Başvuru Sıla Temel tarafından oluşturuldu.", DateTime.UtcNow.AddDays(-3));
        AddHistory(application, ApplicationStatus.Pending, ApplicationStatus.Approved, 2,
            application.EvaluationNote, DateTime.UtcNow.AddDays(-2));
        dbContext.CardApplications.Add(application);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static CardApplication CreateApplication(string number, int customerId, int cardTypeId,
        decimal requestedLimit, ApplicationStatus status, DateTime createdAtUtc) => new()
    {
        ApplicationNumber = number, CustomerId = customerId, CardTypeId = cardTypeId, CreatedByUserId = 1,
        RequestedLimit = requestedLimit, Status = status, CreatedAtUtc = createdAtUtc,
        DeliveryMethod = "RegisteredAddress",
        DeliveryAddress = "Demo Mahallesi, Banka Caddesi No: 10 İstanbul",
        DeliveryCity = "İstanbul", DeliveryDistrict = "Ümraniye",
        DeliveryNeighborhood = "Finanskent Mahallesi",
        StatementPreference = "Email", ContactlessEnabled = true, InternetShoppingEnabled = true
    };

    private static async Task EnsureDemoOperationScenariosAsync(
        ApplicationDbContext dbContext, string contentRootPath, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var applications = await dbContext.CardApplications
            .Include(x => x.Customer)
            .Include(x => x.CreditCard).ThenInclude(x => x!.Fulfillment)
            .OrderBy(x => x.Id).ToListAsync(cancellationToken);

        var scores = new[] { 88, 92, 51, 74, 86 };
        for (var index = 0; index < applications.Count; index++)
        {
            var application = applications[index];
            application.PreAssessmentScore = scores[index % scores.Length];
            application.PreAssessmentRiskLevel = application.PreAssessmentScore >= 80 ? "LOW"
                : application.PreAssessmentScore >= 60 ? "MEDIUM" : "HIGH";
            application.PreAssessmentRecommendation = application.PreAssessmentScore >= 80
                ? "APPROVAL_RECOMMENDED" : application.PreAssessmentScore >= 60 ? "MANUAL_REVIEW" : "REVISION_RECOMMENDED";
            application.AssignedOfficerUserId ??= application.Status == ApplicationStatus.Revision ? 3 : 1;
            application.AssignedByUserId ??= 2;
            application.AssignedAtUtc ??= application.CreatedAtUtc.AddMinutes(10);
            application.IdentityDocumentConfirmed = true;
            application.IncomeDocumentConfirmed = true;
            application.ResidenceDocumentConfirmed = true;
        }

        var documentDirectory = Path.Combine(contentRootPath, "Storage", "DemoDocuments");
        Directory.CreateDirectory(documentDirectory);
        var documentBytes = System.Text.Encoding.UTF8.GetBytes("KKBYS sunum verisi - örnek müşteri belgesi");
        var documentHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(documentBytes));
        foreach (var application in applications.Where(x => x.Status is ApplicationStatus.Pending or ApplicationStatus.Revision))
        foreach (var type in new[] { "Identity", "Income", "Residence" })
        {
            if (await dbContext.GeneratedDocuments.AnyAsync(x => x.EntityType == "CardApplicationUpload"
                    && x.EntityId == application.Id && x.DocumentType == type, cancellationToken)) continue;
            var filePath = Path.Combine(documentDirectory, $"{application.ApplicationNumber}-{type}.txt");
            await File.WriteAllBytesAsync(filePath, documentBytes, cancellationToken);
            dbContext.GeneratedDocuments.Add(new GeneratedDocument
            {
                EntityType = "CardApplicationUpload", EntityId = application.Id, DocumentType = type,
                FileName = $"{application.ApplicationNumber}-{type}-ornek.txt", StoragePath = filePath,
                Sha256 = documentHash, VerificationStatus = "PendingVerification",
                ExpiresAtUtc = now.AddYears(1)
            });
        }

        var approvedCards = applications.Where(x => x.Status == ApplicationStatus.Approved && x.CreditCard is not null)
            .Select(x => x.CreditCard!).ToList();
        foreach (var card in approvedCards)
        {
            if (card.Fulfillment is not null) continue;
            card.Status = CardStatus.Active;
            card.Fulfillment = new CardFulfillment
            {
                Status = "Delivered", ProductionStartedAtUtc = now.AddDays(-5),
                PrintingStartedAtUtc = now.AddDays(-4), PrintedAtUtc = now.AddDays(-4),
                ShippedAtUtc = now.AddDays(-3), DeliveredAtUtc = now.AddDays(-1),
                EstimatedPrintAtUtc = now.AddDays(-4), EstimatedDeliveryAtUtc = now.AddDays(-1),
                NextTransitionAtUtc = now.AddDays(-1), TrackingNumber = $"KRG{card.Id:D8}"
            };
        }

        var primaryCard = approvedCards.FirstOrDefault();
        var holder = applications.Select(x => x.Customer).FirstOrDefault(x => primaryCard is not null
            && x.Id != primaryCard.CardApplication.CustomerId);
        if (primaryCard is not null && holder is not null
            && !await dbContext.SupplementaryCardApplications.AnyAsync(cancellationToken))
        {
            dbContext.SupplementaryCardApplications.Add(new SupplementaryCardApplication
            {
                ApplicationNumber = "EKK-DEMO-0001", PrimaryCustomerId = primaryCard.CardApplication.CustomerId,
                PrimaryCreditCardId = primaryCard.Id, SupplementaryHolderCustomerId = holder.Id,
                Relationship = "Kardeş", RequestedLimit = Math.Min(10_000m, primaryCard.CardLimit / 3),
                DeliveryMethod = "RegisteredAddress", DeliveryAddress = primaryCard.CardApplication.DeliveryAddress,
                Status = "Pending", CreatedByUserId = 1, CreatedAtUtc = now.AddHours(-3), CardStatus = "NotCreated"
            });
        }

        if (approvedCards.Count > 0 && approvedCards[0].LimitIncreaseStatus is null)
        {
            approvedCards[0].LimitChangeType = "Increase";
            approvedCards[0].RequestedNewLimit = approvedCards[0].CardLimit + 10_000m;
            approvedCards[0].LimitIncreaseStatus = "Pending";
            approvedCards[0].LimitIncreaseRequestedAtUtc = now.AddHours(-2);
        }
        if (approvedCards.Count > 1 && approvedCards[1].LimitIncreaseStatus is null)
        {
            approvedCards[1].LimitChangeType = "Decrease";
            approvedCards[1].RequestedNewLimit = approvedCards[1].CardLimit - 5_000m;
            approvedCards[1].CardLimit -= 5_000m;
            approvedCards[1].LimitIncreaseStatus = "Approved";
            approvedCards[1].LimitIncreaseRequestedAtUtc = now.AddDays(-1);
            approvedCards[1].LimitIncreaseEvaluatedAtUtc = now.AddDays(-1).AddMinutes(1);
            approvedCards[1].LimitIncreaseEvaluationNote = "Müşteri talebiyle anında uygulanan limit azaltımı.";
            approvedCards[1].LimitIncreaseEvaluatedByUserId = 1;
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureCustomerSchemaAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var columns = await GetColumnsAsync(dbContext, "Customers", cancellationToken);
        var additions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["PhoneCountryCode"] = "TEXT NOT NULL DEFAULT '+90'",
            ["IsPhoneVerified"] = "INTEGER NOT NULL DEFAULT 0",
            ["PhoneVerificationCodeHash"] = "TEXT NULL",
            ["PhoneVerificationExpiresAtUtc"] = "TEXT NULL",
            ["PhoneVerificationFailedAttempts"] = "INTEGER NOT NULL DEFAULT 0",
            ["IsEmailVerified"] = "INTEGER NOT NULL DEFAULT 0",
            ["EmailVerificationCodeHash"] = "TEXT NULL",
            ["EmailVerificationExpiresAtUtc"] = "TEXT NULL",
            ["EmailVerificationFailedAttempts"] = "INTEGER NOT NULL DEFAULT 0",
            ["BirthDate"] = "TEXT NULL",
            ["Gender"] = "TEXT NOT NULL DEFAULT 'Belirtilmedi'",
            ["EducationLevel"] = "TEXT NOT NULL DEFAULT 'Belirtilmedi'",
            ["Occupation"] = "TEXT NOT NULL DEFAULT 'Belirtilmedi'",
            ["City"] = "TEXT NOT NULL DEFAULT ''",
            ["District"] = "TEXT NOT NULL DEFAULT ''",
            ["Neighborhood"] = "TEXT NOT NULL DEFAULT ''",
            ["Address"] = "TEXT NOT NULL DEFAULT ''",
            ["CreditScore"] = "INTEGER NOT NULL DEFAULT 0"
            ,["EmploymentStatus"] = "TEXT NOT NULL DEFAULT 'Aktif'"
        };
        foreach (var addition in additions.Where(x => !columns.Contains(x.Key)))
        {
            var sql = $"ALTER TABLE Customers ADD COLUMN {addition.Key} {addition.Value};";
            await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        }
        await dbContext.Database.ExecuteSqlRawAsync(
            "UPDATE Customers SET PhoneCountryCode = '+90', PhoneNumber = SUBSTR(PhoneNumber, 2) WHERE PhoneCountryCode = '+90' AND LENGTH(PhoneNumber) = 11 AND PhoneNumber LIKE '0%';",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS IX_Customers_EmailAddress ON Customers (EmailAddress);",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS IX_Customers_PhoneCountryCode_PhoneNumber ON Customers (PhoneCountryCode, PhoneNumber);",
            cancellationToken);
    }

    private static async Task EnsureOtherBankCardSchemaAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS OtherBankCards (
                Id INTEGER NOT NULL CONSTRAINT PK_OtherBankCards PRIMARY KEY AUTOINCREMENT,
                CustomerId INTEGER NOT NULL,
                BankName TEXT NOT NULL,
                MaskedCardNumber TEXT NULL,
                CardLimit TEXT NOT NULL,
                IsActive INTEGER NOT NULL DEFAULT 1,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NULL,
                CONSTRAINT FK_OtherBankCards_Customers_CustomerId
                    FOREIGN KEY (CustomerId) REFERENCES Customers (Id) ON DELETE CASCADE
            );
            """, cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_OtherBankCards_CustomerId ON OtherBankCards (CustomerId);",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync("""
            INSERT INTO OtherBankCards
                (CustomerId, BankName, MaskedCardNumber, CardLimit, IsActive, CreatedAtUtc)
            SELECT Id, 'Beyan Edilen Toplam', NULL, OtherBankTotalCardLimit, 1, CURRENT_TIMESTAMP
            FROM Customers c
            WHERE OtherBankTotalCardLimit > 0
              AND NOT EXISTS (SELECT 1 FROM OtherBankCards o WHERE o.CustomerId = c.Id);
            """, cancellationToken);
    }

    private static async Task EnsureCustomerAddressSchemaAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS CustomerAddresses (
                Id INTEGER NOT NULL CONSTRAINT PK_CustomerAddresses PRIMARY KEY AUTOINCREMENT,
                CustomerId INTEGER NOT NULL,
                Name TEXT NOT NULL,
                City TEXT NOT NULL,
                District TEXT NOT NULL,
                Neighborhood TEXT NOT NULL,
                Street TEXT NOT NULL,
                Avenue TEXT NULL,
                BuildingNo TEXT NOT NULL,
                ApartmentNo TEXT NULL,
                Floor TEXT NULL,
                PostalCode TEXT NOT NULL,
                FullAddress TEXT NOT NULL,
                IsDefault INTEGER NOT NULL DEFAULT 0,
                IsActive INTEGER NOT NULL DEFAULT 1,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NULL,
                CONSTRAINT FK_CustomerAddresses_Customers_CustomerId
                    FOREIGN KEY (CustomerId) REFERENCES Customers (Id) ON DELETE CASCADE
            );
            """, cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "DROP INDEX IF EXISTS IX_CustomerAddresses_CustomerId_Name;", cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_CustomerAddresses_CustomerId_Name ON CustomerAddresses (CustomerId, Name);",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync("""
            INSERT INTO CustomerAddresses
                (CustomerId, Name, City, District, Neighborhood, Street, BuildingNo,
                 PostalCode, FullAddress, IsDefault, IsActive, CreatedAtUtc)
            SELECT Id, 'Ev Adresi', City, District, Neighborhood, Address, '—', '00000',
                   Address, 1, 1, CURRENT_TIMESTAMP
            FROM Customers c
            WHERE LENGTH(TRIM(Address)) > 0
              AND NOT EXISTS (SELECT 1 FROM CustomerAddresses a WHERE a.CustomerId = c.Id);
            """, cancellationToken);
    }

    private static async Task EnsureApplicationSchemaAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var columns = await GetColumnsAsync(dbContext, "CardApplications", cancellationToken);
        var additions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DeliveryMethod"] = "TEXT NOT NULL DEFAULT 'RegisteredAddress'",
            ["DeliveryAddress"] = "TEXT NOT NULL DEFAULT 'Kayıtlı müşteri adresi'",
            ["DeliveryCity"] = "TEXT NULL",
            ["DeliveryDistrict"] = "TEXT NULL",
            ["DeliveryNeighborhood"] = "TEXT NULL",
            ["DeliveryRecipientName"] = "TEXT NULL",
            ["DeliveryPhone"] = "TEXT NULL",
            ["DeliveryBranch"] = "TEXT NULL",
            ["StatementPreference"] = "TEXT NOT NULL DEFAULT 'Email'",
            ["ContactlessEnabled"] = "INTEGER NOT NULL DEFAULT 1",
            ["InternetShoppingEnabled"] = "INTEGER NOT NULL DEFAULT 1",
            ["ApplicationNote"] = "TEXT NULL"
            ,["StatementDay"] = "INTEGER NOT NULL DEFAULT 15"
            ,["AutomaticLimitIncreaseEnabled"] = "INTEGER NOT NULL DEFAULT 0"
            ,["AutomaticLimitIncreaseConsentAtUtc"] = "TEXT NULL"
            ,["IdentityDocumentConfirmed"] = "INTEGER NOT NULL DEFAULT 0"
            ,["IncomeDocumentConfirmed"] = "INTEGER NOT NULL DEFAULT 0"
            ,["ResidenceDocumentConfirmed"] = "INTEGER NOT NULL DEFAULT 0"
            ,["DuplicateWarningAcknowledged"] = "INTEGER NOT NULL DEFAULT 0"
            ,["PreAssessmentScore"] = "INTEGER NOT NULL DEFAULT 0"
            ,["PreAssessmentRiskLevel"] = "TEXT NOT NULL DEFAULT 'MEDIUM'"
            ,["PreAssessmentRecommendation"] = "TEXT NOT NULL DEFAULT 'MANUAL_REVIEW'"
            ,["PreAssessmentPositiveFactorsJson"] = "TEXT NOT NULL DEFAULT '[]'"
            ,["PreAssessmentRiskFactorsJson"] = "TEXT NOT NULL DEFAULT '[]'"
            ,["AssignedOfficerUserId"] = "INTEGER NULL"
            ,["AssignedByUserId"] = "INTEGER NULL"
            ,["AssignedAtUtc"] = "TEXT NULL"
            ,["RequiresSecondApproval"] = "INTEGER NOT NULL DEFAULT 0"
            ,["FirstApprovedByUserId"] = "INTEGER NULL"
            ,["FirstApprovedAtUtc"] = "TEXT NULL"
            ,["FirstApprovalNote"] = "TEXT NULL"
            ,["AutoAssignmentCompleted"] = "INTEGER NOT NULL DEFAULT 0"
            ,["LastAssignmentReason"] = "TEXT NULL"
            ,["EscalationLevel"] = "INTEGER NOT NULL DEFAULT 0"
            ,["LastEscalatedAtUtc"] = "TEXT NULL"
            ,["CancelledByUserId"] = "INTEGER NULL"
            ,["CancelledAtUtc"] = "TEXT NULL"
            ,["CancellationReason"] = "TEXT NULL"
        };
        foreach (var addition in additions.Where(x => !columns.Contains(x.Key)))
        {
            var sql = $"ALTER TABLE CardApplications ADD COLUMN {addition.Key} {addition.Value};";
            await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        }
        await dbContext.Database.ExecuteSqlRawAsync("""
            UPDATE CardApplications
            SET AutoAssignmentCompleted = 1,
                LastAssignmentReason = COALESCE(LastAssignmentReason, 'Mevcut sorumlu memur korundu.')
            WHERE AssignedOfficerUserId IS NOT NULL AND AutoAssignmentCompleted = 0;
            """, cancellationToken);
    }

    private static async Task EnsureCreditCardSchemaAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var columns = await GetColumnsAsync(dbContext, "CreditCards", cancellationToken);
        var additions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["RequestedNewLimit"] = "TEXT NULL",
            ["LimitIncreaseStatus"] = "TEXT NULL",
            ["LimitIncreaseRequestedAtUtc"] = "TEXT NULL",
            ["LimitIncreaseEvaluationNote"] = "TEXT NULL",
            ["LimitIncreaseEvaluatedAtUtc"] = "TEXT NULL",
            ["LimitIncreaseEvaluatedByUserId"] = "INTEGER NULL"
            ,["LimitChangeType"] = "TEXT NULL"
        };
        foreach (var addition in additions.Where(x => !columns.Contains(x.Key)))
        {
            var sql = $"ALTER TABLE CreditCards ADD COLUMN {addition.Key} {addition.Value};";
            await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        }
        // İlk prototipte enum varsayılanı 0 olarak kalan kartları geçerli Pasif durumuna taşır.
        await dbContext.Database.ExecuteSqlRawAsync(
            "UPDATE CreditCards SET Status = 1 WHERE Status = 0;", cancellationToken);
    }

    private static async Task EnsurePlatformV2SchemaAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userColumns = await GetColumnsAsync(dbContext, "Users", cancellationToken);
        var userAdditions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CorporateEmail"] = "TEXT NOT NULL DEFAULT ''",
            ["PhoneNumber"] = "TEXT NOT NULL DEFAULT ''",
            ["Title"] = "TEXT NOT NULL DEFAULT ''",
            ["Department"] = "TEXT NOT NULL DEFAULT ''",
            ["Branch"] = "TEXT NOT NULL DEFAULT ''",
            ["ProfilePhotoUrl"] = "TEXT NULL",
            ["TwoFactorEnabled"] = "INTEGER NOT NULL DEFAULT 0",
            ["PasswordChangedAtUtc"] = "TEXT NULL",
            ["NotifyApplicationEvents"] = "INTEGER NOT NULL DEFAULT 1",
            ["NotifySlaWarnings"] = "INTEGER NOT NULL DEFAULT 1",
            ["NotifySecurityEvents"] = "INTEGER NOT NULL DEFAULT 1",
            ["ActiveSessionId"] = "TEXT NULL",
            ["ActiveSessionExpiresAtUtc"] = "TEXT NULL",
            ["LastActivityAtUtc"] = "TEXT NULL"
        };
        foreach (var addition in userAdditions.Where(x => !userColumns.Contains(x.Key)))
            await dbContext.Database.ExecuteSqlRawAsync(
                $"ALTER TABLE Users ADD COLUMN {addition.Key} {addition.Value};", cancellationToken);

        var statements = new[]
        {
            """
            CREATE TABLE IF NOT EXISTS Notifications (
                Id INTEGER NOT NULL CONSTRAINT PK_Notifications PRIMARY KEY AUTOINCREMENT,
                UserId INTEGER NOT NULL, Type TEXT NOT NULL, Title TEXT NOT NULL, Message TEXT NOT NULL,
                Link TEXT NULL, IsRead INTEGER NOT NULL DEFAULT 0, CreatedAtUtc TEXT NOT NULL, UpdatedAtUtc TEXT NULL,
                CONSTRAINT FK_Notifications_Users_UserId FOREIGN KEY (UserId) REFERENCES Users (Id) ON DELETE CASCADE);
            """,
            "CREATE INDEX IF NOT EXISTS IX_Notifications_UserId_IsRead ON Notifications (UserId, IsRead);",
            """
            CREATE TABLE IF NOT EXISTS ChatMessages (
                Id INTEGER NOT NULL CONSTRAINT PK_ChatMessages PRIMARY KEY AUTOINCREMENT,
                SenderUserId INTEGER NOT NULL, RecipientUserId INTEGER NOT NULL, CardApplicationId INTEGER NULL,
                Message TEXT NOT NULL, IsRead INTEGER NOT NULL DEFAULT 0, ReadAtUtc TEXT NULL,
                CreatedAtUtc TEXT NOT NULL, UpdatedAtUtc TEXT NULL,
                CONSTRAINT FK_ChatMessages_Users_SenderUserId FOREIGN KEY (SenderUserId) REFERENCES Users (Id) ON DELETE RESTRICT,
                CONSTRAINT FK_ChatMessages_Users_RecipientUserId FOREIGN KEY (RecipientUserId) REFERENCES Users (Id) ON DELETE RESTRICT,
                CONSTRAINT FK_ChatMessages_CardApplications_CardApplicationId FOREIGN KEY (CardApplicationId) REFERENCES CardApplications (Id) ON DELETE SET NULL);
            """,
            "CREATE INDEX IF NOT EXISTS IX_ChatMessages_SenderUserId_RecipientUserId_CreatedAtUtc ON ChatMessages (SenderUserId, RecipientUserId, CreatedAtUtc);",
            """
            CREATE TABLE IF NOT EXISTS CustomerConsents (
                Id INTEGER NOT NULL CONSTRAINT PK_CustomerConsents PRIMARY KEY AUTOINCREMENT,
                CustomerId INTEGER NOT NULL, ConsentType TEXT NOT NULL, TextVersion TEXT NOT NULL,
                IsGranted INTEGER NOT NULL, Channel TEXT NOT NULL, CapturedByUserId INTEGER NOT NULL,
                CapturedAtUtc TEXT NOT NULL, WithdrawnAtUtc TEXT NULL, CreatedAtUtc TEXT NOT NULL, UpdatedAtUtc TEXT NULL,
                CONSTRAINT FK_CustomerConsents_Customers_CustomerId FOREIGN KEY (CustomerId) REFERENCES Customers (Id) ON DELETE CASCADE,
                CONSTRAINT FK_CustomerConsents_Users_CapturedByUserId FOREIGN KEY (CapturedByUserId) REFERENCES Users (Id) ON DELETE RESTRICT);
            """,
            "CREATE INDEX IF NOT EXISTS IX_CustomerConsents_CustomerId_ConsentType_CreatedAtUtc ON CustomerConsents (CustomerId, ConsentType, CreatedAtUtc);",
            """
            CREATE TABLE IF NOT EXISTS ApplicationRevisionSnapshots (
                Id INTEGER NOT NULL CONSTRAINT PK_ApplicationRevisionSnapshots PRIMARY KEY AUTOINCREMENT,
                CardApplicationId INTEGER NOT NULL, RevisionNumber INTEGER NOT NULL, Stage TEXT NOT NULL,
                DataJson TEXT NOT NULL, CapturedByUserId INTEGER NOT NULL, CreatedAtUtc TEXT NOT NULL, UpdatedAtUtc TEXT NULL,
                CONSTRAINT FK_ApplicationRevisionSnapshots_CardApplications_CardApplicationId FOREIGN KEY (CardApplicationId) REFERENCES CardApplications (Id) ON DELETE CASCADE,
                CONSTRAINT FK_ApplicationRevisionSnapshots_Users_CapturedByUserId FOREIGN KEY (CapturedByUserId) REFERENCES Users (Id) ON DELETE RESTRICT);
            """,
            "CREATE INDEX IF NOT EXISTS IX_ApplicationRevisionSnapshots_CardApplicationId_RevisionNumber_Stage ON ApplicationRevisionSnapshots (CardApplicationId, RevisionNumber, Stage);",
            """
            CREATE TABLE IF NOT EXISTS CardFulfillments (
                Id INTEGER NOT NULL CONSTRAINT PK_CardFulfillments PRIMARY KEY AUTOINCREMENT,
                CreditCardId INTEGER NOT NULL, Status TEXT NOT NULL, ProductionStartedAtUtc TEXT NOT NULL,
                PrintingStartedAtUtc TEXT NULL, PrintedAtUtc TEXT NULL, ShippedAtUtc TEXT NULL, DeliveredAtUtc TEXT NULL,
                EstimatedPrintAtUtc TEXT NOT NULL, EstimatedDeliveryAtUtc TEXT NOT NULL, NextTransitionAtUtc TEXT NOT NULL,
                TrackingNumber TEXT NULL, CreatedAtUtc TEXT NOT NULL, UpdatedAtUtc TEXT NULL,
                CONSTRAINT FK_CardFulfillments_CreditCards_CreditCardId FOREIGN KEY (CreditCardId) REFERENCES CreditCards (Id) ON DELETE CASCADE);
            """,
            "CREATE UNIQUE INDEX IF NOT EXISTS IX_CardFulfillments_CreditCardId ON CardFulfillments (CreditCardId);",
            """
            CREATE TABLE IF NOT EXISTS SupplementaryCardApplications (
                Id INTEGER NOT NULL CONSTRAINT PK_SupplementaryCardApplications PRIMARY KEY AUTOINCREMENT,
                ApplicationNumber TEXT NOT NULL, PrimaryCustomerId INTEGER NOT NULL, PrimaryCreditCardId INTEGER NOT NULL,
                SupplementaryHolderCustomerId INTEGER NOT NULL, Relationship TEXT NOT NULL, RequestedLimit TEXT NOT NULL,
                DeliveryMethod TEXT NOT NULL DEFAULT 'RegisteredAddress', DeliveryAddress TEXT NOT NULL DEFAULT '',
                Status TEXT NOT NULL, CreatedByUserId INTEGER NOT NULL, EvaluatedByUserId INTEGER NULL,
                EvaluationNote TEXT NULL, EvaluatedAtUtc TEXT NULL, MaskedCardNumber TEXT NULL, IssuedAtUtc TEXT NULL,
                CardStatus TEXT NOT NULL DEFAULT 'NotCreated', FulfillmentStatus TEXT NULL,
                EstimatedPrintAtUtc TEXT NULL, EstimatedDeliveryAtUtc TEXT NULL, NextTransitionAtUtc TEXT NULL,
                PrintingStartedAtUtc TEXT NULL, ShippedAtUtc TEXT NULL, DeliveredAtUtc TEXT NULL, TrackingNumber TEXT NULL,
                CreatedAtUtc TEXT NOT NULL, UpdatedAtUtc TEXT NULL,
                CONSTRAINT FK_SupplementaryCardApplications_Customers_PrimaryCustomerId FOREIGN KEY (PrimaryCustomerId) REFERENCES Customers (Id) ON DELETE RESTRICT,
                CONSTRAINT FK_SupplementaryCardApplications_CreditCards_PrimaryCreditCardId FOREIGN KEY (PrimaryCreditCardId) REFERENCES CreditCards (Id) ON DELETE RESTRICT,
                CONSTRAINT FK_SupplementaryCardApplications_Customers_SupplementaryHolderCustomerId FOREIGN KEY (SupplementaryHolderCustomerId) REFERENCES Customers (Id) ON DELETE RESTRICT,
                CONSTRAINT FK_SupplementaryCardApplications_Users_CreatedByUserId FOREIGN KEY (CreatedByUserId) REFERENCES Users (Id) ON DELETE RESTRICT,
                CONSTRAINT FK_SupplementaryCardApplications_Users_EvaluatedByUserId FOREIGN KEY (EvaluatedByUserId) REFERENCES Users (Id) ON DELETE RESTRICT);
            """,
            "CREATE UNIQUE INDEX IF NOT EXISTS IX_SupplementaryCardApplications_ApplicationNumber ON SupplementaryCardApplications (ApplicationNumber);",
            """
            CREATE TABLE IF NOT EXISTS GeneratedDocuments (
                Id INTEGER NOT NULL CONSTRAINT PK_GeneratedDocuments PRIMARY KEY AUTOINCREMENT,
                EntityType TEXT NOT NULL, EntityId INTEGER NOT NULL, DocumentType TEXT NOT NULL,
                FileName TEXT NOT NULL, StoragePath TEXT NOT NULL, Sha256 TEXT NOT NULL,
                EmailStatus TEXT NOT NULL, EmailedAtUtc TEXT NULL, EmailFailureReason TEXT NULL,
                CreatedAtUtc TEXT NOT NULL, UpdatedAtUtc TEXT NULL);
            """,
            "CREATE INDEX IF NOT EXISTS IX_GeneratedDocuments_EntityType_EntityId_DocumentType ON GeneratedDocuments (EntityType, EntityId, DocumentType);",
            """
            CREATE TABLE IF NOT EXISTS CustomerAccounts (
                Id INTEGER NOT NULL CONSTRAINT PK_CustomerAccounts PRIMARY KEY AUTOINCREMENT,
                CustomerId INTEGER NOT NULL, Username TEXT NOT NULL, PasswordHash TEXT NOT NULL,
                IsActive INTEGER NOT NULL DEFAULT 1, FailedLoginCount INTEGER NOT NULL DEFAULT 0,
                LockoutEndUtc TEXT NULL, LastLoginAtUtc TEXT NULL, CreatedAtUtc TEXT NOT NULL, UpdatedAtUtc TEXT NULL,
                CONSTRAINT FK_CustomerAccounts_Customers_CustomerId FOREIGN KEY (CustomerId) REFERENCES Customers (Id) ON DELETE CASCADE);
            """,
            "CREATE UNIQUE INDEX IF NOT EXISTS IX_CustomerAccounts_CustomerId ON CustomerAccounts (CustomerId);",
            "CREATE UNIQUE INDEX IF NOT EXISTS IX_CustomerAccounts_Username ON CustomerAccounts (Username);"
        };
        foreach (var statement in statements)
            await dbContext.Database.ExecuteSqlRawAsync(statement, cancellationToken);
        var documentColumns = await GetColumnsAsync(dbContext, "GeneratedDocuments", cancellationToken);
        var documentAdditions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["VerificationStatus"] = "TEXT NOT NULL DEFAULT 'PendingVerification'",
            ["VerifiedByUserId"] = "INTEGER NULL",
            ["VerifiedAtUtc"] = "TEXT NULL",
            ["VerificationNote"] = "TEXT NULL",
            ["ExpiresAtUtc"] = "TEXT NULL"
        };
        foreach (var addition in documentAdditions.Where(x => !documentColumns.Contains(x.Key)))
            await dbContext.Database.ExecuteSqlRawAsync(
                $"ALTER TABLE GeneratedDocuments ADD COLUMN {addition.Key} {addition.Value};", cancellationToken);
        var supplementaryColumns = await GetColumnsAsync(dbContext, "SupplementaryCardApplications", cancellationToken);
        if (!supplementaryColumns.Contains("MaskedCardNumber"))
            await dbContext.Database.ExecuteSqlRawAsync(
                "ALTER TABLE SupplementaryCardApplications ADD COLUMN MaskedCardNumber TEXT NULL;", cancellationToken);
        if (!supplementaryColumns.Contains("IssuedAtUtc"))
            await dbContext.Database.ExecuteSqlRawAsync(
                "ALTER TABLE SupplementaryCardApplications ADD COLUMN IssuedAtUtc TEXT NULL;", cancellationToken);
        if (!supplementaryColumns.Contains("DeliveryMethod"))
            await dbContext.Database.ExecuteSqlRawAsync(
                "ALTER TABLE SupplementaryCardApplications ADD COLUMN DeliveryMethod TEXT NOT NULL DEFAULT 'RegisteredAddress';", cancellationToken);
        if (!supplementaryColumns.Contains("DeliveryAddress"))
            await dbContext.Database.ExecuteSqlRawAsync(
                "ALTER TABLE SupplementaryCardApplications ADD COLUMN DeliveryAddress TEXT NOT NULL DEFAULT '';", cancellationToken);
        var fulfillmentColumns = new Dictionary<string, string>
        {
            ["CardStatus"] = "TEXT NOT NULL DEFAULT 'NotCreated'",
            ["FulfillmentStatus"] = "TEXT NULL",
            ["EstimatedPrintAtUtc"] = "TEXT NULL",
            ["EstimatedDeliveryAtUtc"] = "TEXT NULL",
            ["NextTransitionAtUtc"] = "TEXT NULL",
            ["PrintingStartedAtUtc"] = "TEXT NULL",
            ["ShippedAtUtc"] = "TEXT NULL",
            ["DeliveredAtUtc"] = "TEXT NULL",
            ["TrackingNumber"] = "TEXT NULL"
        };
        foreach (var addition in fulfillmentColumns.Where(x => !supplementaryColumns.Contains(x.Key)))
            await dbContext.Database.ExecuteSqlRawAsync(
                $"ALTER TABLE SupplementaryCardApplications ADD COLUMN {addition.Key} {addition.Value};", cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync("""
            UPDATE SupplementaryCardApplications SET
              CardStatus = 'Inactive',
              FulfillmentStatus = 'Production',
              EstimatedPrintAtUtc = COALESCE(EstimatedPrintAtUtc, datetime('now', '+1 day')),
              EstimatedDeliveryAtUtc = COALESCE(EstimatedDeliveryAtUtc, datetime('now', '+4 days')),
              NextTransitionAtUtc = COALESCE(NextTransitionAtUtc, datetime('now', '+1 minute'))
            WHERE Status = 'Approved' AND (CardStatus = 'NotCreated' OR FulfillmentStatus IS NULL);
            """, cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync("""
            UPDATE Users SET
              CorporateEmail = CASE WHEN RegistrationNumber = 'KBP000001' THEN 'sila.temel@kartbasvuru.local' ELSE 'ayse.yilmaz@kartbasvuru.local' END,
              PhoneNumber = CASE WHEN RegistrationNumber = 'KBP000001' THEN '+905551234567' ELSE '+905559876543' END,
              Title = CASE WHEN RegistrationNumber = 'KBP000001' THEN 'Kart Başvuru Memuru' ELSE 'Kart Başvuru Müdürü' END,
              Department = 'Bireysel Bankacılık',
              Branch = 'Finanskent Şubesi',
              PasswordChangedAtUtc = COALESCE(PasswordChangedAtUtc, CURRENT_TIMESTAMP)
            WHERE CorporateEmail = '';
            """, cancellationToken);
    }

    private static async Task EnsureCustomerPortalAccountsAsync(
        ApplicationDbContext dbContext,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var customerPassword = GetRequiredDemoPassword(configuration, "CustomerPassword");
        var demoCustomers = await dbContext.Customers
            .Where(x => x.IsActive)
            .ToListAsync(cancellationToken);
        foreach (var customer in demoCustomers)
        {
            if (!await dbContext.CustomerAccounts.AnyAsync(x => x.CustomerId == customer.Id, cancellationToken))
                dbContext.CustomerAccounts.Add(new CustomerAccount
                {
                    CustomerId = customer.Id, Username = customer.CustomerNumber,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(customerPassword)
                });
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureSqliteHealthAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync(cancellationToken);

        try
        {
            await using var quickCheck = connection.CreateCommand();
            quickCheck.CommandText = "PRAGMA quick_check;";
            var quickCheckResult = Convert.ToString(
                await quickCheck.ExecuteScalarAsync(cancellationToken));
            if (!string.Equals(quickCheckResult, "ok", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"SQLite bütünlük denetimi başarısız: {quickCheckResult ?? "sonuç alınamadı"}. " +
                    "Uygulamayı durdurup sağlam bir veritabanı yedeği kullanın.");

            await using var foreignKeyCheck = connection.CreateCommand();
            foreignKeyCheck.CommandText = "PRAGMA foreign_key_check;";
            await using var reader = await foreignKeyCheck.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
                throw new InvalidOperationException(
                    $"SQLite yabancı anahtar ihlali bulundu. Tablo: {reader.GetString(0)}, satır: {reader.GetInt64(1)}.");
            await reader.DisposeAsync();

            await using var configure = connection.CreateCommand();
            configure.CommandText = "PRAGMA foreign_keys=ON; PRAGMA busy_timeout=30000; PRAGMA journal_mode=WAL;";
            await configure.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (shouldClose)
                await dbContext.Database.CloseConnectionAsync();
        }
    }

    private static async Task<HashSet<string>> GetColumnsAsync(
        ApplicationDbContext dbContext,
        string tableName,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info('{tableName}');";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            columns.Add(reader.GetString(1));
        await reader.DisposeAsync();
        await dbContext.Database.CloseConnectionAsync();
        return columns;
    }

    private static async Task EnsureRevisionDemoAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var revision = await dbContext.CardApplications
            .Include(x => x.Histories)
            .FirstOrDefaultAsync(x => x.ApplicationNumber == "KKB-DEMO-0004", cancellationToken);
        if (revision is null || revision.Status == ApplicationStatus.Revision) return;

        var previousStatus = revision.Status;
        revision.Status = ApplicationStatus.Revision;
        revision.EvaluatedByUserId = 2;
        revision.EvaluatedAtUtc = DateTime.UtcNow.AddHours(-1);
        revision.EvaluationNote = "Talep edilen limit ve müşteri gelir bilgisi yeniden kontrol edilmelidir.";
        revision.Histories.Add(new ApplicationHistory
        {
            PreviousStatus = previousStatus,
            NewStatus = ApplicationStatus.Revision,
            ChangedByUserId = 2,
            Description = revision.EvaluationNote,
            CreatedAtUtc = revision.EvaluatedAtUtc.Value
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureCustomerDemoDetailsAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var customers = await dbContext.Customers
            .Where(x => x.CustomerNumber.StartsWith("CUS-DEMO-"))
            .ToListAsync(cancellationToken);
        var changed = false;
        foreach (var customer in customers.Where(x => string.IsNullOrWhiteSpace(x.Address)))
        {
            customer.BirthDate = new DateOnly(1992, 5, 12);
            customer.Gender = customer.FirstName is "Elif" or "Fatma" ? "Kadın" : "Erkek";
            customer.EducationLevel = "Lisans";
            customer.Occupation = "Mühendis";
            customer.City = "İstanbul";
            customer.District = "Ümraniye";
            customer.Neighborhood = "Finanskent Mahallesi";
            customer.Address = "Finanskent Mahallesi, Örnek Sokak No: 1, Ümraniye / İstanbul";
            customer.CreditScore = 1650;
            changed = true;
        }
        foreach (var customer in customers.Where(x =>
                     string.IsNullOrWhiteSpace(x.Gender) || x.Gender == "Belirtilmedi"))
        {
            customer.Gender = customer.FirstName is "Elif" or "Fatma" ? "Kadın" : "Erkek";
            changed = true;
        }
        foreach (var customer in customers.Where(x => !x.IsPhoneVerified || !x.IsEmailVerified))
        {
            customer.IsPhoneVerified = true;
            customer.IsEmailVerified = true;
            changed = true;
        }
        var registeredApplications = await dbContext.CardApplications
            .Include(x => x.Customer)
            .Where(x => x.DeliveryMethod == "RegisteredAddress"
                && x.DeliveryAddress == "Kayıtlı müşteri adresi")
            .ToListAsync(cancellationToken);
        foreach (var application in registeredApplications.Where(x => !string.IsNullOrWhiteSpace(x.Customer.Address)))
        {
            application.DeliveryAddress = application.Customer.Address;
            application.DeliveryCity = application.Customer.City;
            application.DeliveryDistrict = application.Customer.District;
            application.DeliveryNeighborhood = application.Customer.Neighborhood;
            application.DeliveryRecipientName = $"{application.Customer.FirstName} {application.Customer.LastName}";
            application.DeliveryPhone = application.Customer.PhoneNumber;
            changed = true;
        }
        if (changed)
            await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void AddHistory(CardApplication application, ApplicationStatus? previousStatus,
        ApplicationStatus newStatus, int userId, string? description, DateTime createdAtUtc) =>
        application.Histories.Add(new ApplicationHistory
        {
            PreviousStatus = previousStatus, NewStatus = newStatus, ChangedByUserId = userId,
            Description = description, CreatedAtUtc = createdAtUtc
        });
}
