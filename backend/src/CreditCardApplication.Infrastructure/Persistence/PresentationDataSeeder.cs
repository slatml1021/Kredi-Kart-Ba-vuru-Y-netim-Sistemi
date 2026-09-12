using CreditCardApplication.Application.Applications;
using CreditCardApplication.Domain.Entities;
using CreditCardApplication.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CreditCardApplication.Infrastructure.Persistence;

/// <summary>
/// Creates a deterministic, presentation-sized development dataset. The dataset intentionally
/// contains multiple process stages while keeping all identity and relationship rules valid.
/// </summary>
public static class PresentationDataSeeder
{
    public const int CustomerCount = 200;
    public const int OfficerCount = 50;
    public const int ManagerCount = 5;

    private static readonly int[] OfficerIds = [1, 3, 5, .. Enumerable.Range(6, 47)];
    private static readonly int[] ManagerIds = [2, 4, 53, 54, 55];

    private static readonly string[] FirstNames =
    [
        "Elif", "Fatma", "Gökhan", "Hakan", "Ece", "Aslı", "Emre", "Leyla", "Kerem", "Buse",
        "Mert", "Ceren", "Onur", "Selin", "Deniz", "Ayşe", "Burak", "Derya", "Can", "Zeynep"
    ];

    private static readonly string[] LastNames =
    [
        "Erdem", "Fidan", "Güler", "Hızır", "Aydın", "Kaya", "Demir", "Çetin", "Yıldız", "Aksoy",
        "Koç", "Şahin", "Karaca", "Güneş", "Arslan", "Yılmaz", "Öztürk", "Korkmaz", "Polat", "Acar"
    ];

    private static readonly AddressSeed[] Addresses =
    [
        new("İstanbul", "Ümraniye", "Finanskent Mahallesi", "Banka Caddesi", "34760"),
        new("İstanbul", "Kadıköy", "Caferağa Mahallesi", "Moda Caddesi", "34710"),
        new("Ankara", "Çankaya", "Kızılay Mahallesi", "Atatürk Bulvarı", "06420"),
        new("İzmir", "Konak", "Alsancak Mahallesi", "Kıbrıs Şehitleri Caddesi", "35220"),
        new("Bursa", "Nilüfer", "İhsaniye Mahallesi", "Fatih Sultan Mehmet Bulvarı", "16130"),
        new("Antalya", "Muratpaşa", "Fener Mahallesi", "Tekelioğlu Caddesi", "07160"),
        new("Adana", "Seyhan", "Reşatbey Mahallesi", "Atatürk Caddesi", "01120"),
        new("Konya", "Selçuklu", "Yazır Mahallesi", "Beyhekim Caddesi", "42250"),
        new("Kocaeli", "İzmit", "Yahyakaptan Mahallesi", "Şehit Ergün Köncü Caddesi", "41050"),
        new("Eskişehir", "Tepebaşı", "Hoşnudiye Mahallesi", "İsmet İnönü Caddesi", "26130")
    ];

    public static async Task EnsureCardCatalogAsync(
        ApplicationDbContext db,
        CancellationToken cancellationToken = default)
    {
        var definitions = new[]
        {
            new CardProductSeed(1, "Classic Visa", "45000101", 5_000m, 50_000m),
            new CardProductSeed(2, "Gold Mastercard", "54000102", 15_000m, 150_000m),
            new CardProductSeed(3, "Platinum Visa", "45000103", 30_000m, 300_000m),
            new CardProductSeed(4, "Platinum Plus Visa", "45000104", 50_000m, 500_000m),
            new CardProductSeed(5, "Classic TROY", "97920101", 5_000m, 50_000m),
            new CardProductSeed(6, "Gold TROY", "97920102", 15_000m, 150_000m),
            new CardProductSeed(7, "Classic Mastercard", "54000101", 5_000m, 50_000m),
            new CardProductSeed(8, "Gold Visa", "45000102", 15_000m, 150_000m),
            new CardProductSeed(9, "Platinum Mastercard", "54000103", 30_000m, 300_000m),
            new CardProductSeed(10, "Platinum TROY", "97920103", 30_000m, 300_000m),
            new CardProductSeed(11, "Platinum Plus Mastercard", "54000104", 50_000m, 500_000m),
            new CardProductSeed(12, "Platinum Plus TROY", "97920104", 50_000m, 500_000m)
        };

        var existing = await db.CardTypes.OrderBy(x => x.Id).ToListAsync(cancellationToken);
        foreach (var product in definitions.Where(product => existing.Any(x => x.Id == product.Id)))
        {
            var entity = existing.Single(x => x.Id == product.Id);
            entity.Name = product.Name;
            entity.Bin = product.Bin;
            entity.MinimumLimit = product.MinimumLimit;
            entity.MaximumLimit = product.MaximumLimit;
            entity.IsActive = true;
        }
        await db.SaveChangesAsync(cancellationToken);

        var existingIds = existing.Select(x => x.Id).ToHashSet();
        db.CardTypes.AddRange(definitions.Where(x => !existingIds.Contains(x.Id)).Select(product => new CardType
        {
            Id = product.Id,
            Name = product.Name,
            Bin = product.Bin,
            MinimumLimit = product.MinimumLimit,
            MaximumLimit = product.MaximumLimit,
            IsActive = true
        }));
        await db.SaveChangesAsync(cancellationToken);
    }

    public static async Task EnsureStaffAsync(
        ApplicationDbContext db,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        var definitions = BuildStaffDefinitions().ToList();
        foreach (var definition in definitions)
        {
            var password = GetPassword(configuration, definition.PasswordKey);
            var user = await db.Users.Include(x => x.UserRoles)
                .SingleOrDefaultAsync(x => x.RegistrationNumber == definition.RegistrationNumber, cancellationToken);
            if (user is null)
            {
                db.Users.Add(new User
                {
                    Id = definition.Id,
                    RegistrationNumber = definition.RegistrationNumber,
                    FullName = definition.FullName,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                    CorporateEmail = definition.Email,
                    PhoneNumber = definition.Phone,
                    Title = definition.RoleId == 2 ? "Kart Başvuru Müdürü" : "Kart Başvuru Memuru",
                    Department = "Kart Başvuru Operasyonları",
                    Branch = definition.Branch,
                    PasswordChangedAtUtc = DateTime.UtcNow,
                    TwoFactorEnabled = definition.RoleId == 2,
                    UserRoles = [new UserRole { RoleId = definition.RoleId }]
                });
                continue;
            }

            user.FullName = definition.FullName;
            user.CorporateEmail = definition.Email;
            user.PhoneNumber = definition.Phone;
            user.Title = definition.RoleId == 2 ? "Kart Başvuru Müdürü" : "Kart Başvuru Memuru";
            user.Department = "Kart Başvuru Operasyonları";
            user.Branch = definition.Branch;
            user.IsActive = true;
            if (!user.PasswordHash.StartsWith("$2", StringComparison.Ordinal)
                || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
            foreach (var obsoleteRole in user.UserRoles
                         .Where(role => role.RoleId != definition.RoleId)
                         .ToList())
                db.UserRoles.Remove(obsoleteRole);
            if (user.UserRoles.All(role => role.RoleId != definition.RoleId))
                user.UserRoles.Add(new UserRole { RoleId = definition.RoleId });
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    public static async Task SeedDatasetAsync(
        ApplicationDbContext db,
        CancellationToken cancellationToken = default)
    {
        if (await db.Customers.AnyAsync(cancellationToken)) return;

        var now = DateTime.UtcNow;
        var cardTypes = await db.CardTypes.AsNoTracking().OrderBy(x => x.Id).ToListAsync(cancellationToken);
        if (cardTypes.Count < 12)
            throw new InvalidOperationException("Sunum verisi için 12 aktif kart ürünü bulunmalıdır.");

        var customers = Enumerable.Range(1, CustomerCount)
            .Select(CreateCustomer)
            .ToList();
        db.Customers.AddRange(customers);
        await db.SaveChangesAsync(cancellationToken);

        var usedCardNumbers = new HashSet<string>(StringComparer.Ordinal);
        var applications = new List<CardApplication>(CustomerCount);
        for (var index = 0; index < customers.Count; index++)
        {
            var customer = customers[index];
            var cardType = cardTypes[index % cardTypes.Count];
            var createdAt = now.AddDays(-(index * 11 % 720)).AddHours(-(index % 22));
            var approvedAt = createdAt.AddHours(2 + index % 8);
            var approvedLimit = CalculateApprovedLimit(customer, cardType.MinimumLimit ?? 1_000m, cardType.MaximumLimit ?? 500_000m, index);
            var officerId = OfficerIds[index % OfficerIds.Length];
            var managerId = ManagerIds[index % ManagerIds.Length];
            var maskedCardNumber = GenerateUniqueMaskedCardNumber(cardType.Bin, usedCardNumbers);
            var isDelivered = index % 5 is 0 or 1 or 2;

            var application = new CardApplication
            {
                ApplicationNumber = $"KKB-{createdAt:yyyy}-{index + 1:D6}",
                CustomerId = customer.Id,
                CardTypeId = cardType.Id,
                CreatedByUserId = officerId,
                AssignedOfficerUserId = officerId,
                AssignedByUserId = managerId,
                AssignedAtUtc = createdAt.AddMinutes(15),
                AutoAssignmentCompleted = index % 2 == 0,
                LastAssignmentReason = index % 2 == 0 ? "Şube ve iş yükü kurallarına göre otomatik atandı." : "Müdür tarafından memura atandı.",
                RequestedLimit = approvedLimit,
                ApprovedLimit = approvedLimit,
                DeliveryMethod = index % 3 == 0 ? "Branch" : index % 3 == 1 ? "RegisteredAddress" : "DifferentAddress",
                DeliveryAddress = customer.Address,
                DeliveryCity = customer.City,
                DeliveryDistrict = customer.District,
                DeliveryNeighborhood = customer.Neighborhood,
                DeliveryRecipientName = $"{customer.FirstName} {customer.LastName}",
                DeliveryPhone = $"{customer.PhoneCountryCode}{customer.PhoneNumber}",
                DeliveryBranch = index % 3 == 0 ? $"{customer.District} Şubesi" : null,
                StatementPreference = index % 3 == 0 ? "Email" : index % 3 == 1 ? "Mobile" : "Paper",
                StatementDay = new[] { 5, 10, 15, 20, 25 }[index % 5],
                ContactlessEnabled = index % 9 != 0,
                InternetShoppingEnabled = index % 7 != 0,
                AutomaticLimitIncreaseEnabled = index % 4 == 0,
                AutomaticLimitIncreaseConsentAtUtc = index % 4 == 0 ? createdAt : null,
                IdentityDocumentConfirmed = true,
                IncomeDocumentConfirmed = true,
                ResidenceDocumentConfirmed = true,
                PreAssessmentScore = 55 + index * 13 % 45,
                PreAssessmentRiskLevel = RiskLevel(55 + index * 13 % 45),
                PreAssessmentRecommendation = Recommendation(55 + index * 13 % 45),
                PreAssessmentPositiveFactorsJson = "[\"İletişim bilgileri doğrulandı\",\"Gelir bilgisi mevcut\"]",
                PreAssessmentRiskFactorsJson = index % 4 == 0 ? "[\"Talep edilen limit manuel inceleme sınırında\"]" : "[]",
                ApplicationNote = "Sunum veri setinde oluşturulan onaylı ana kart başvurusu.",
                Status = ApplicationStatus.Approved,
                EvaluationNote = "Gelir, risk ve ürün kuralları doğrulandı.",
                EvaluatedByUserId = managerId,
                EvaluatedAtUtc = approvedAt,
                CreatedAtUtc = createdAt,
                Histories =
                [
                    new ApplicationHistory
                    {
                        NewStatus = ApplicationStatus.Pending, ChangedByUserId = officerId,
                        Description = "Başvuru memur tarafından oluşturuldu.", CreatedAtUtc = createdAt
                    },
                    new ApplicationHistory
                    {
                        PreviousStatus = ApplicationStatus.Pending, NewStatus = ApplicationStatus.Approved,
                        ChangedByUserId = managerId, Description = "Başvuru müdür tarafından onaylandı.",
                        CreatedAtUtc = approvedAt
                    }
                ],
                CreditCard = new CreditCard
                {
                    MaskedCardNumber = maskedCardNumber,
                    CardLimit = approvedLimit,
                    Status = isDelivered ? CardStatus.Active : CardStatus.Inactive,
                    IssueDate = DateOnly.FromDateTime(approvedAt),
                    ExpiryDate = DateOnly.FromDateTime(approvedAt.AddYears(4)),
                    CreatedAtUtc = approvedAt,
                    Fulfillment = CreateFulfillment(index, approvedAt, isDelivered)
                }
            };
            ConfigureLimitScenario(application.CreditCard, index, approvedAt);
            applications.Add(application);
        }
        db.CardApplications.AddRange(applications);
        await db.SaveChangesAsync(cancellationToken);

        var relationships = new[] { "Eş", "Anne", "Baba", "Çocuk", "Kardeş", "Diğer" };
        var supplementaryApplications = new List<SupplementaryCardApplication>(CustomerCount);
        for (var index = 0; index < applications.Count; index++)
        {
            var application = applications[index];
            var primaryCard = application.CreditCard!;
            var holder = customers[(index + 1) % customers.Count];
            var cardType = cardTypes.Single(x => x.Id == application.CardTypeId);
            var issuedAt = application.EvaluatedAtUtc!.Value.AddHours(3);
            var delivered = index % 4 != 3;
            var supplementaryMaskedCardNumber = GenerateUniqueMaskedCardNumber(cardType.Bin, usedCardNumbers);
            supplementaryApplications.Add(new SupplementaryCardApplication
            {
                ApplicationNumber = $"EKK-{application.CreatedAtUtc:yyyy}-{index + 1:D6}",
                PrimaryCustomerId = application.CustomerId,
                PrimaryCreditCardId = primaryCard.Id,
                SupplementaryHolderCustomerId = holder.Id,
                Relationship = relationships[index % relationships.Length],
                RequestedLimit = Math.Max(1_000m, Math.Round(primaryCard.CardLimit * (20 + index % 31) / 100m / 500m) * 500m),
                DeliveryMethod = index % 3 == 0 ? "Branch" : index % 3 == 1 ? "RegisteredAddress" : "DifferentAddress",
                DeliveryAddress = holder.Address,
                Status = "Approved",
                CreatedByUserId = application.CreatedByUserId,
                EvaluatedByUserId = application.EvaluatedByUserId,
                EvaluationNote = "Ana kart limiti ve ek kart sahibi uygunluğu kontrol edildi.",
                EvaluatedAtUtc = issuedAt,
                MaskedCardNumber = supplementaryMaskedCardNumber,
                IssuedAtUtc = issuedAt,
                CardStatus = delivered ? "Active" : "Inactive",
                FulfillmentStatus = delivered ? "Delivered" : "Printing",
                EstimatedPrintAtUtc = issuedAt.AddDays(1),
                EstimatedDeliveryAtUtc = issuedAt.AddDays(4),
                NextTransitionAtUtc = delivered ? issuedAt.AddDays(4) : now.AddHours(4),
                PrintingStartedAtUtc = issuedAt.AddDays(1),
                ShippedAtUtc = delivered ? issuedAt.AddDays(2) : null,
                DeliveredAtUtc = delivered ? issuedAt.AddDays(4) : null,
                TrackingNumber = delivered ? $"EKKKRG{index + 1:D8}" : null,
                CreatedAtUtc = application.CreatedAtUtc.AddHours(1)
            });
        }
        db.SupplementaryCardApplications.AddRange(supplementaryApplications);

        var scenarioApplications = BuildOpenAndRejectedScenarios(customers, cardTypes, now);
        db.CardApplications.AddRange(scenarioApplications);

        db.Notifications.AddRange(Enumerable.Range(0, 55).Select(index => new Notification
        {
            UserId = index < 50 ? OfficerIds[index] : ManagerIds[index - 50],
            Type = index % 3 == 0 ? "SlaWarning" : "Application",
            Title = index % 3 == 0 ? "SLA bilgilendirmesi" : "Başvuru güncellendi",
            Message = $"Sunum veri setindeki {index + 1:D3} numaralı operasyon bildirimi.",
            Link = index < 50 ? "/officer/applications" : "/manager/all-applications",
            IsRead = index % 4 == 0,
            CreatedAtUtc = now.AddMinutes(-index * 7)
        }));

        db.ChatMessages.AddRange(Enumerable.Range(0, 20).Select(index => new ChatMessage
        {
            SenderUserId = index % 2 == 0 ? 2 : OfficerIds[index % OfficerIds.Length],
            RecipientUserId = index % 2 == 0 ? OfficerIds[index % OfficerIds.Length] : 2,
            CardApplicationId = applications[index].Id,
            Message = $"{applications[index].ApplicationNumber} başvurusu için kurum içi süreç bilgilendirmesi.",
            IsRead = index % 3 != 0,
            ReadAtUtc = index % 3 != 0 ? now.AddMinutes(-index) : null,
            CreatedAtUtc = now.AddMinutes(-index * 11)
        }));

        await db.SaveChangesAsync(cancellationToken);
    }

    public static bool IsValidTurkishIdentityNumber(string value)
    {
        if (value.Length != 11 || value[0] == '0' || value.Any(x => !char.IsDigit(x))) return false;
        var digits = value.Select(x => x - '0').ToArray();
        var tenth = ((digits[0] + digits[2] + digits[4] + digits[6] + digits[8]) * 7
                     - (digits[1] + digits[3] + digits[5] + digits[7])) % 10;
        if (tenth < 0) tenth += 10;
        return digits[9] == tenth && digits[10] == digits.Take(10).Sum() % 10;
    }

    public static string GenerateTurkishIdentityNumber(int sequence)
    {
        if (sequence is < 1 or > 899_999_999) throw new ArgumentOutOfRangeException(nameof(sequence));
        var firstNine = (100_000_000 + sequence - 1).ToString("D9");
        var digits = firstNine.Select(x => x - '0').ToList();
        var tenth = ((digits[0] + digits[2] + digits[4] + digits[6] + digits[8]) * 7
                     - (digits[1] + digits[3] + digits[5] + digits[7])) % 10;
        if (tenth < 0) tenth += 10;
        digits.Add(tenth);
        digits.Add(digits.Sum() % 10);
        return string.Concat(digits);
    }

    private static Customer CreateCustomer(int sequence)
    {
        var index = sequence - 1;
        var address = Addresses[index % Addresses.Length];
        var firstName = FirstNames[index % FirstNames.Length];
        var lastName = LastNames[(index / FirstNames.Length + index) % LastNames.Length];
        var income = 25_000m + index % 16 * 7_500m;
        var otherBankLimit = index % 6 * 5_000m;
        var buildingNo = (index % 90 + 1).ToString();
        var apartmentNo = (index % 24 + 1).ToString();
        var fullAddress = $"{address.Neighborhood}, {address.Street}, Bina No: {buildingNo}, Daire: {apartmentNo}, {address.PostalCode}, {address.District} / {address.City}";
        return new Customer
        {
            CustomerNumber = $"MUS{sequence:D6}",
            NationalIdentityNumber = GenerateTurkishIdentityNumber(sequence),
            FirstName = firstName,
            LastName = lastName,
            PhoneCountryCode = "+90",
            PhoneNumber = $"555{sequence:D7}",
            IsPhoneVerified = true,
            EmailAddress = $"musteri{sequence:D3}@example.com",
            IsEmailVerified = true,
            BirthDate = new DateOnly(1958 + index % 47, index % 12 + 1, index % 27 + 1),
            Gender = index % 2 == 0 ? "Kadın" : "Erkek",
            EducationLevel = new[] { "Lise", "Ön Lisans", "Lisans", "Yüksek Lisans" }[index % 4],
            Occupation = new[] { "Mühendis", "Öğretmen", "Memur", "Serbest Meslek", "Emekli" }[index % 5],
            EmploymentStatus = index % 9 == 0 ? "Emekli" : "Aktif",
            City = address.City,
            District = address.District,
            Neighborhood = address.Neighborhood,
            Address = fullAddress,
            MonthlyNetIncome = income,
            OtherBankTotalCardLimit = otherBankLimit,
            CreditScore = 900 + index * 37 % 901,
            IsActive = true,
            SavedAddresses =
            [
                new CustomerAddress
                {
                    Name = "Ev Adresi", City = address.City, District = address.District,
                    Neighborhood = address.Neighborhood, Street = address.Street,
                    BuildingNo = buildingNo, ApartmentNo = apartmentNo, Floor = (index % 12).ToString(),
                    PostalCode = address.PostalCode, FullAddress = fullAddress, IsDefault = true
                }
            ],
            OtherBankCards = otherBankLimit == 0 ? [] :
            [
                new OtherBankCard
                {
                    BankName = new[] { "Diğer Banka A", "Diğer Banka B", "Diğer Banka C" }[index % 3],
                    MaskedCardNumber = $"**** {7000 + index:D4}", CardLimit = otherBankLimit, IsActive = true
                }
            ]
        };
    }

    private static IEnumerable<CardApplication> BuildOpenAndRejectedScenarios(
        IReadOnlyList<Customer> customers, IReadOnlyList<CardType> cardTypes, DateTime now)
    {
        for (var index = 0; index < 40; index++)
        {
            var status = (index % 4) switch
            {
                0 => ApplicationStatus.Pending,
                1 => ApplicationStatus.Revision,
                2 => ApplicationStatus.Rejected,
                _ => ApplicationStatus.Cancelled
            };
            var customer = customers[index];
            var cardType = cardTypes[(index + 5) % cardTypes.Count];
            var officer = OfficerIds[(index + 7) % OfficerIds.Length];
            var manager = ManagerIds[index % ManagerIds.Length];
            var createdAt = now.AddHours(-(index + 1) * 3);
            var application = new CardApplication
            {
                ApplicationNumber = $"KKB-SEN-{index + 1:D6}", CustomerId = customer.Id,
                CardTypeId = cardType.Id, CreatedByUserId = officer,
                AssignedOfficerUserId = index % 5 == 0 ? null : officer,
                AssignedByUserId = index % 5 == 0 ? null : manager,
                AssignedAtUtc = index % 5 == 0 ? null : createdAt.AddMinutes(10),
                RequestedLimit = Math.Min(cardType.MaximumLimit ?? 500_000m, Math.Max(cardType.MinimumLimit ?? 1_000m, customer.MonthlyNetIncome)),
                DeliveryMethod = "RegisteredAddress", DeliveryAddress = customer.Address,
                DeliveryCity = customer.City, DeliveryDistrict = customer.District,
                DeliveryNeighborhood = customer.Neighborhood, StatementPreference = "Email", StatementDay = 15,
                ContactlessEnabled = true, InternetShoppingEnabled = true,
                IdentityDocumentConfirmed = true, IncomeDocumentConfirmed = true, ResidenceDocumentConfirmed = true,
                PreAssessmentScore = 50 + index % 45, PreAssessmentRiskLevel = RiskLevel(50 + index % 45),
                PreAssessmentRecommendation = Recommendation(50 + index % 45),
                PreAssessmentPositiveFactorsJson = "[\"Müşteri profili tamamlandı\"]",
                PreAssessmentRiskFactorsJson = status == ApplicationStatus.Rejected ? "[\"Risk kriterleri karşılanmadı\"]" : "[]",
                Status = status, CreatedAtUtc = createdAt,
                EvaluatedByUserId = status is ApplicationStatus.Rejected or ApplicationStatus.Cancelled or ApplicationStatus.Revision ? manager : null,
                EvaluatedAtUtc = status is ApplicationStatus.Rejected or ApplicationStatus.Cancelled or ApplicationStatus.Revision ? createdAt.AddHours(2) : null,
                EvaluationNote = status switch
                {
                    ApplicationStatus.Revision => "Gelir belgesinin güncellenmesi istendi.",
                    ApplicationStatus.Rejected => "Risk kriterleri karşılanmadı.",
                    ApplicationStatus.Cancelled => "Müşteri talebiyle iptal edildi.",
                    _ => null
                },
                Histories =
                [
                    new ApplicationHistory
                    {
                        NewStatus = ApplicationStatus.Pending, ChangedByUserId = officer,
                        Description = "Senaryo başvurusu oluşturuldu.", CreatedAtUtc = createdAt
                    }
                ]
            };
            if (status != ApplicationStatus.Pending)
                application.Histories.Add(new ApplicationHistory
                {
                    PreviousStatus = ApplicationStatus.Pending, NewStatus = status, ChangedByUserId = manager,
                    Description = application.EvaluationNote, CreatedAtUtc = createdAt.AddHours(2)
                });
            yield return application;
        }
    }

    private static CardFulfillment CreateFulfillment(int index, DateTime approvedAt, bool delivered)
    {
        var stage = index % 5;
        return new CardFulfillment
        {
            Status = delivered ? "Delivered" : stage == 3 ? "Printing" : "Production",
            ProductionStartedAtUtc = approvedAt,
            PrintingStartedAtUtc = stage >= 1 ? approvedAt.AddDays(1) : null,
            PrintedAtUtc = stage >= 2 ? approvedAt.AddDays(1).AddHours(4) : null,
            ShippedAtUtc = delivered ? approvedAt.AddDays(2) : null,
            DeliveredAtUtc = delivered ? approvedAt.AddDays(4) : null,
            EstimatedPrintAtUtc = approvedAt.AddDays(1),
            EstimatedDeliveryAtUtc = approvedAt.AddDays(4),
            NextTransitionAtUtc = delivered ? approvedAt.AddDays(4) : DateTime.UtcNow.AddHours(4),
            TrackingNumber = delivered ? $"KRG{index + 1:D8}" : null,
            CreatedAtUtc = approvedAt
        };
    }

    private static void ConfigureLimitScenario(CreditCard card, int index, DateTime approvedAt)
    {
        switch (index % 20)
        {
            case 0:
                card.LimitChangeType = "Increase";
                card.RequestedNewLimit = card.CardLimit + 5_000m;
                card.LimitIncreaseStatus = "Pending";
                card.LimitIncreaseRequestedAtUtc = approvedAt.AddDays(7);
                break;
            case 1:
                card.LimitChangeType = "Increase";
                card.RequestedNewLimit = card.CardLimit + 5_000m;
                card.LimitIncreaseStatus = "Approved";
                card.LimitIncreaseRequestedAtUtc = approvedAt.AddDays(5);
                card.LimitIncreaseEvaluatedAtUtc = approvedAt.AddDays(5).AddHours(1);
                card.LimitIncreaseEvaluationNote = "Limit artırım talebi gelir ve risk kurallarına uygun bulundu.";
                card.LimitIncreaseEvaluatedByUserId = ManagerIds[index % ManagerIds.Length];
                break;
            case 2:
                card.LimitChangeType = "Decrease";
                card.RequestedNewLimit = Math.Max(1_000m, card.CardLimit - 5_000m);
                card.LimitIncreaseStatus = "Approved";
                card.LimitIncreaseRequestedAtUtc = approvedAt.AddDays(4);
                card.LimitIncreaseEvaluatedAtUtc = approvedAt.AddDays(4);
                card.LimitIncreaseEvaluationNote = "Müşteri talebiyle otomatik uygulanan limit azaltımı.";
                card.LimitIncreaseEvaluatedByUserId = OfficerIds[index % OfficerIds.Length];
                card.CardLimit = card.RequestedNewLimit.Value;
                break;
            case 3:
                card.LimitChangeType = "Increase";
                card.RequestedNewLimit = card.CardLimit + 20_000m;
                card.LimitIncreaseStatus = "Rejected";
                card.LimitIncreaseRequestedAtUtc = approvedAt.AddDays(3);
                card.LimitIncreaseEvaluatedAtUtc = approvedAt.AddDays(3).AddHours(2);
                card.LimitIncreaseEvaluationNote = "Talep müşteri azami limitini aştığı için reddedildi.";
                card.LimitIncreaseEvaluatedByUserId = ManagerIds[index % ManagerIds.Length];
                break;
        }
    }

    private static decimal CalculateApprovedLimit(Customer customer, decimal productMinimum, decimal productMaximum, int index)
    {
        var available = Math.Max(productMinimum, customer.MonthlyNetIncome * 3 - customer.OtherBankTotalCardLimit);
        var productBounded = Math.Min(productMaximum, available);
        var ratio = 60 + index % 31;
        return Math.Max(productMinimum, Math.Floor(productBounded * ratio / 100m / 500m) * 500m);
    }

    private static string GenerateUniqueMaskedCardNumber(string bin, ISet<string> used)
    {
        string masked;
        do
        {
            var pan = PaymentCardNumberGenerator.Generate(bin);
            masked = PaymentCardNumberGenerator.Mask(pan, bin.Length);
        } while (!used.Add(masked));
        return masked;
    }

    private static string RiskLevel(int score) => score >= 80 ? "LOW" : score >= 65 ? "MEDIUM" : "HIGH";
    private static string Recommendation(int score) => score >= 80 ? "APPROVAL_RECOMMENDED" : score >= 65 ? "MANUAL_REVIEW" : "REVISION_RECOMMENDED";

    private static IEnumerable<StaffSeed> BuildStaffDefinitions()
    {
        var branches = new[] { "Finanskent Şubesi", "Kadıköy Şubesi", "Ataşehir Şubesi", "Ankara Merkez Şubesi", "İzmir Merkez Şubesi" };
        for (var id = 1; id <= 55; id++)
        {
            var isManager = ManagerIds.Contains(id);
            var fullName = id switch
            {
                1 => "Sıla Temel", 2 => "Ayşe Yılmaz", 3 => "Mert Kaya", 4 => "Deniz Arslan", 5 => "Ece Şahin",
                _ => $"{FirstNames[(id + 3) % FirstNames.Length]} {LastNames[(id * 3) % LastNames.Length]} {id:D2}"
            };
            var passwordKey = id switch
            {
                1 => "OfficerPassword", 2 => "ManagerPassword", 3 => "ScenarioOfficerPassword",
                4 => "ScenarioManagerPassword", 5 => "BranchOfficerPassword",
                _ when isManager => "ScenarioManagerPassword", _ => "ScenarioOfficerPassword"
            };
            yield return new StaffSeed(id, $"KBP{id:D6}", fullName, isManager ? 2 : 1, passwordKey,
                $"kbp{id:D6}@kartbasvuru.local", $"+90554{id:D7}", isManager ? "Genel Müdürlük" : branches[id % branches.Length]);
        }
    }

    private static string GetPassword(IConfiguration configuration, string key) =>
        configuration[$"DemoData:{key}"] is { Length: >= 10 } password
            ? password
            : throw new InvalidOperationException($"DemoData:{key} en az 10 karakter olmalıdır.");

    private sealed record AddressSeed(string City, string District, string Neighborhood, string Street, string PostalCode);
    private sealed record CardProductSeed(int Id, string Name, string Bin, decimal MinimumLimit, decimal MaximumLimit);
    private sealed record StaffSeed(int Id, string RegistrationNumber, string FullName, int RoleId,
        string PasswordKey, string Email, string Phone, string Branch);
}
