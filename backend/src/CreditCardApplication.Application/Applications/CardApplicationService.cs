using CreditCardApplication.Application.Services;
using CreditCardApplication.Application.Customers;
using CreditCardApplication.Domain.Entities;
using CreditCardApplication.Domain.Enums;
using System.Text.Json;
using System.Collections.Concurrent;

namespace CreditCardApplication.Application.Applications;

public sealed class CardApplicationService(
    ICardApplicationRepository applicationRepository,
    LimitCalculator limitCalculator)
{
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> CustomerLocks = new();
    public async Task<IReadOnlyList<CardTypeResponse>> GetCardTypesAsync(CancellationToken cancellationToken)
    {
        var cardTypes = await applicationRepository.GetActiveCardTypesAsync(cancellationToken);
        return cardTypes.Select(MapCardType).ToList();
    }

    public async Task<CardApplicationResponse?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var application = await applicationRepository.GetByIdAsync(id, cancellationToken);
        return application is null ? null : Map(application);
    }

    public Task<bool> IsOwnedByAsync(int applicationId, int officerUserId, CancellationToken cancellationToken) =>
        applicationRepository.IsCreatedByAsync(applicationId, officerUserId, cancellationToken);

    public async Task<CardApplicationDetailResponse?> GetDetailByIdAsync(int id, CancellationToken cancellationToken)
    {
        var application = await applicationRepository.GetDetailByIdAsync(id, cancellationToken);
        if (application is null) return null;
        return new CardApplicationDetailResponse(
            Map(application),
            application.ApprovedLimit,
            application.EvaluationNote,
            application.CreditCard?.Id,
            application.Histories.OrderBy(x => x.CreatedAtUtc).Select(x => new ApplicationHistoryResponse(
                x.PreviousStatus?.ToString(), x.NewStatus.ToString(), x.Description,
                x.ChangedByUser.FullName, x.CreatedAtUtc)).ToList());
    }

    public async Task<IReadOnlyList<CardApplicationResponse>> GetByOfficerAsync(int officerUserId, CancellationToken cancellationToken) =>
        (await applicationRepository.GetByOfficerAsync(officerUserId, cancellationToken)).Select(x => Map(x)).ToList();

    public async Task<IReadOnlyList<CardApplicationResponse>> GetPendingAsync(CancellationToken cancellationToken)
    {
        var applications = await applicationRepository.GetPendingAsync(cancellationToken);
        var result = new List<CardApplicationResponse>();
        foreach (var application in applications)
        {
            var blocked = await applicationRepository.HasEarlierOpenApplicationAsync(
                application.CustomerId, application.Id, application.CreatedAtUtc, cancellationToken);
            result.Add(Map(application, isEvaluationBlocked: blocked));
        }
        return result;
    }

    public async Task<IReadOnlyList<CardApplicationResponse>> GetAllAsync(CancellationToken cancellationToken) =>
        (await applicationRepository.GetAllAsync(cancellationToken)).Select(x => Map(x)).ToList();

    public async Task<CardApplicationResponse> CreateAsync(
        CreateCardApplicationRequest request,
        int createdByUserId,
        CancellationToken cancellationToken)
    {
        var customer = await applicationRepository.GetCustomerAsync(request.CustomerId, cancellationToken)
            ?? throw new ArgumentException("Müşteri bulunamadı.");
        if (!customer.IsActive)
            throw new InvalidOperationException("Pasif müşteri için kart başvurusu oluşturulamaz.");
        if (!await applicationRepository.HasCurrentGrantedConsentAsync(customer.Id, "KVKK", cancellationToken))
            throw new InvalidOperationException("Kart başvurusu oluşturmak için güncel KVKK onayı bulunmalıdır.");
        var missingFields = CustomerService.GetMissingProfileFields(customer);
        if (missingFields.Count > 0)
            throw new InvalidOperationException(
                $"Kart başvurusu öncesinde müşteri bilgileri tamamlanmalıdır: {string.Join(", ", missingFields)}.");

        var cardType = await applicationRepository.GetCardTypeAsync(request.CardTypeId, cancellationToken)
            ?? throw new ArgumentException("Seçilen kart tipi bulunamadı veya kullanıma kapalı.");
        if (await applicationRepository.HasApprovedCardTypeAsync(customer.Id, cardType.Id, cancellationToken))
            throw new InvalidOperationException($"Müşterinin aktif/onaylı bir {cardType.Name} kartı bulunmaktadır. Aynı kart tipine yeniden başvurulamaz.");
        var recentDuplicate = await applicationRepository.GetRecentSameCardTypeAsync(
            customer.Id, cardType.Id, DateTime.UtcNow.AddDays(-15), cancellationToken);
        if (recentDuplicate?.Status is ApplicationStatus.Pending or ApplicationStatus.Revision)
            throw new InvalidOperationException(
                $"Bu müşterinin {recentDuplicate.ApplicationNumber} numaralı {cardType.Name} kart başvurusu hâlen değerlendirmededir. Sonuçlanmadan aynı kart tipi için yeni başvuru oluşturulamaz.");

        var ownBankLimit = await applicationRepository.GetApprovedCardLimitTotalAsync(customer.Id, cancellationToken);
        var calculation = limitCalculator.Calculate(
            customer.MonthlyNetIncome, customer.OtherBankTotalCardLimit, ownBankLimit);
        ValidateRequestedLimit(request.RequestedLimit, cardType);
        var delivery = ValidateDelivery(
            request.DeliveryMethod, request.DeliveryAddress, request.DeliveryCity,
            request.DeliveryDistrict, request.DeliveryNeighborhood, request.DeliveryRecipientName,
            request.DeliveryPhone, request.DeliveryBranch, customer);
        var statementPreference = NormalizeStatementPreference(request.StatementPreference);
        ValidateStatementDay(request.StatementDay);
        ValidateApplicationNote(request.ApplicationNote);
        ValidateDocuments(
            request.IdentityDocumentConfirmed,
            request.IncomeDocumentConfirmed,
            request.ResidenceDocumentConfirmed);

        var application = new CardApplication
        {
            ApplicationNumber = $"KKB-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            CustomerId = customer.Id,
            Customer = customer,
            CardTypeId = cardType.Id,
            CardType = cardType,
            CreatedByUserId = createdByUserId,
            // Başvuruyu oluşturan memur ilk sorumlu olarak atanır. Müdür daha sonra
            // iş yükü ekranından bu atamayı başka bir memura devredebilir.
            AssignedOfficerUserId = createdByUserId,
            AssignedAtUtc = DateTime.UtcNow,
            AutoAssignmentCompleted = true,
            LastAssignmentReason = "Başvuruyu oluşturan memura otomatik atandı.",
            RequestedLimit = request.RequestedLimit,
            DeliveryMethod = delivery.Method,
            DeliveryAddress = delivery.Address,
            DeliveryCity = delivery.City,
            DeliveryDistrict = delivery.District,
            DeliveryNeighborhood = delivery.Neighborhood,
            DeliveryRecipientName = delivery.RecipientName,
            DeliveryPhone = delivery.Phone,
            DeliveryBranch = delivery.Branch,
            StatementPreference = statementPreference,
            StatementDay = request.StatementDay,
            ContactlessEnabled = request.ContactlessEnabled,
            InternetShoppingEnabled = request.InternetShoppingEnabled,
            AutomaticLimitIncreaseEnabled = request.AutomaticLimitIncreaseEnabled,
            AutomaticLimitIncreaseConsentAtUtc = request.AutomaticLimitIncreaseEnabled ? DateTime.UtcNow : null,
            IdentityDocumentConfirmed = request.IdentityDocumentConfirmed,
            IncomeDocumentConfirmed = request.IncomeDocumentConfirmed,
            ResidenceDocumentConfirmed = request.ResidenceDocumentConfirmed,
            DuplicateWarningAcknowledged = request.DuplicateWarningAcknowledged,
            ApplicationNote = request.ApplicationNote?.Trim(),
            Status = ApplicationStatus.Pending
        };
        var history = new ApplicationHistory
        {
            CardApplication = application,
            NewStatus = ApplicationStatus.Pending,
            ChangedByUserId = createdByUserId,
            Description = "Başvuru memur tarafından oluşturuldu."
        };

        await applicationRepository.AddAsync(application, history, cancellationToken);
        return Map(application, calculation.AvailableLimit);
    }

    public async Task<CardApplicationResponse> EvaluateAsync(
        int applicationId,
        EvaluateCardApplicationRequest request,
        int managerUserId,
        CancellationToken cancellationToken)
    {
        var initial = await applicationRepository.GetByIdAsync(applicationId, cancellationToken)
            ?? throw new ArgumentException("Başvuru bulunamadı.");
        var gate = CustomerLocks.GetOrAdd(initial.CustomerId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
        var application = await applicationRepository.GetTrackedByIdAsync(applicationId, cancellationToken)
            ?? throw new ArgumentException("Başvuru bulunamadı.");
        if (application.Status != ApplicationStatus.Pending)
            throw new InvalidOperationException("Yalnızca bekleyen başvurular değerlendirilebilir.");
        if (await applicationRepository.HasEarlierOpenApplicationAsync(
                application.CustomerId, application.Id, application.CreatedAtUtc, cancellationToken))
            throw new InvalidOperationException(
                "Bu başvuru kuyrukta bekliyor. Müşterinin daha önce oluşturulan başvurusu sonuçlandırılmalıdır.");

        if (!Enum.TryParse<ApplicationStatus>(request.Decision, true, out var newStatus)
            || newStatus is not (ApplicationStatus.Approved or ApplicationStatus.Rejected or ApplicationStatus.Revision))
            throw new ArgumentException("Karar Approved, Rejected veya Revision olmalıdır.");
        if (newStatus is ApplicationStatus.Revision or ApplicationStatus.Rejected
            && string.IsNullOrWhiteSpace(request.Note))
            throw new ArgumentException(newStatus == ApplicationStatus.Revision
                ? "Revizyon kararı için açıklama zorunludur."
                : "Ret kararı için açıklama zorunludur.");
        if (newStatus == ApplicationStatus.Approved && (!request.ApprovedLimit.HasValue || request.ApprovedLimit <= 0))
            throw new ArgumentException("Onaylanan limit sıfırdan büyük olmalıdır.");

        var ownBankLimit = await applicationRepository.GetApprovedCardLimitTotalAsync(
            application.CustomerId, cancellationToken);
        var availableLimit = limitCalculator.Calculate(
            application.Customer.MonthlyNetIncome,
            application.Customer.OtherBankTotalCardLimit,
            ownBankLimit).AvailableLimit;
        if (newStatus == ApplicationStatus.Approved)
        {
            if (!await applicationRepository.HasRequiredDocumentsAsync(application.Id, cancellationToken))
                throw new InvalidOperationException(
                    "Başvuru onaylanmadan önce kimlik, gelir ve ikametgah belgeleri sisteme yüklenmelidir.");
            if (await applicationRepository.HasApprovedCardTypeAsync(
                    application.CustomerId, application.CardTypeId, cancellationToken))
                throw new InvalidOperationException(
                    $"Müşterinin onaylı bir {application.CardType.Name} kartı bulunmaktadır. Aynı kart tipi ikinci kez onaylanamaz.");
            ValidateApprovedLimit(request.ApprovedLimit!.Value, availableLimit, application.CardType);
            if (application.CreditCard is not null)
                throw new InvalidOperationException("Bu başvuru için zaten bir kart oluşturulmuş.");
            var needsDualControl = request.ApprovedLimit.Value > 100_000m
                || application.PreAssessmentRiskLevel.Equals("HIGH", StringComparison.OrdinalIgnoreCase);
            if (needsDualControl && !application.FirstApprovedByUserId.HasValue)
            {
                application.RequiresSecondApproval = true;
                application.FirstApprovedByUserId = managerUserId;
                application.FirstApprovedAtUtc = DateTime.UtcNow;
                application.FirstApprovalNote = request.Note?.Trim();
                application.ApprovedLimit = request.ApprovedLimit;
                application.Histories.Add(new ApplicationHistory
                {
                    PreviousStatus = ApplicationStatus.Pending,
                    NewStatus = ApplicationStatus.Pending,
                    ChangedByUserId = managerUserId,
                    Description = $"Birinci müdür onayı verildi. {request.ApprovedLimit.Value:N2} TL için bağımsız ikinci müdür onayı bekleniyor."
                });
                await applicationRepository.SaveChangesAsync(cancellationToken);
                return Map(application, availableLimit);
            }
            if (application.RequiresSecondApproval)
            {
                if (application.FirstApprovedByUserId == managerUserId)
                    throw new InvalidOperationException("İkinci onay, ilk onayı veren müdürden farklı bir müdür tarafından verilmelidir.");
                if (application.ApprovedLimit.HasValue && application.ApprovedLimit.Value != request.ApprovedLimit.Value)
                    throw new InvalidOperationException(
                        $"İkinci onay limiti ilk onaylanan {application.ApprovedLimit.Value:N2} TL ile aynı olmalıdır. Limit değişecekse başvuru revizyona gönderilmelidir.");
            }
            application.ApprovedLimit = request.ApprovedLimit;
            application.CreditCard = CreateDemoCard(application, request.ApprovedLimit.Value);
        }
        else if (newStatus == ApplicationStatus.Revision)
        {
            var revisionNumber = application.RevisionSnapshots.Count == 0
                ? 1 : application.RevisionSnapshots.Max(x => x.RevisionNumber) + 1;
            application.RevisionSnapshots.Add(new ApplicationRevisionSnapshot
            {
                RevisionNumber = revisionNumber, Stage = "Before",
                DataJson = JsonSerializer.Serialize(CreateSnapshot(application)),
                CapturedByUserId = managerUserId
            });
        }

        if (newStatus is ApplicationStatus.Rejected or ApplicationStatus.Revision)
        {
            application.RequiresSecondApproval = false;
            application.FirstApprovedByUserId = null;
            application.FirstApprovedAtUtc = null;
            application.FirstApprovalNote = null;
            application.ApprovedLimit = null;
        }

        var previousStatus = application.Status;
        application.Status = newStatus;
        application.EvaluationNote = request.Note?.Trim();
        application.EvaluatedByUserId = managerUserId;
        application.EvaluatedAtUtc = DateTime.UtcNow;
        application.Histories.Add(new ApplicationHistory
        {
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            ChangedByUserId = managerUserId,
            Description = application.EvaluationNote
        });
        await applicationRepository.SaveChangesAsync(cancellationToken);
        return Map(application, availableLimit);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<CardApplicationResponse> ResubmitAsync(
        int applicationId,
        ResubmitCardApplicationRequest request,
        int officerUserId,
        CancellationToken cancellationToken)
    {
        var application = await applicationRepository.GetTrackedByIdAsync(applicationId, cancellationToken)
            ?? throw new ArgumentException("Başvuru bulunamadı.");
        if (application.CreatedByUserId != officerUserId && application.AssignedOfficerUserId != officerUserId)
            throw new UnauthorizedAccessException("Bu başvuruyu güncelleme yetkiniz yok.");
        if (application.Status != ApplicationStatus.Revision)
            throw new InvalidOperationException("Yalnızca revizyondaki başvurular yeniden gönderilebilir.");

        var cardType = await applicationRepository.GetCardTypeAsync(request.CardTypeId, cancellationToken)
            ?? throw new ArgumentException("Seçilen kart tipi bulunamadı veya kullanıma kapalı.");
        var ownBankLimit = await applicationRepository.GetApprovedCardLimitTotalAsync(
            application.CustomerId, cancellationToken);
        var availableLimit = limitCalculator.Calculate(
            application.Customer.MonthlyNetIncome,
            application.Customer.OtherBankTotalCardLimit,
            ownBankLimit).AvailableLimit;
        ValidateRequestedLimit(request.RequestedLimit, cardType);
        var delivery = ValidateDelivery(
            request.DeliveryMethod, request.DeliveryAddress, request.DeliveryCity,
            request.DeliveryDistrict, request.DeliveryNeighborhood, request.DeliveryRecipientName,
            request.DeliveryPhone, request.DeliveryBranch, application.Customer);
        ValidateApplicationNote(request.ApplicationNote);
        ValidateStatementDay(request.StatementDay);
        ValidateDocuments(
            request.IdentityDocumentConfirmed,
            request.IncomeDocumentConfirmed,
            request.ResidenceDocumentConfirmed);

        var activeRevision = application.RevisionSnapshots
            .Where(x => x.Stage == "Before").OrderByDescending(x => x.RevisionNumber).FirstOrDefault();

        var previousStatus = application.Status;
        application.CardTypeId = cardType.Id;
        application.CardType = cardType;
        application.RequestedLimit = request.RequestedLimit;
        application.DeliveryMethod = delivery.Method;
        application.DeliveryAddress = delivery.Address;
        application.DeliveryCity = delivery.City;
        application.DeliveryDistrict = delivery.District;
        application.DeliveryNeighborhood = delivery.Neighborhood;
        application.DeliveryRecipientName = delivery.RecipientName;
        application.DeliveryPhone = delivery.Phone;
        application.DeliveryBranch = delivery.Branch;
        application.StatementPreference = NormalizeStatementPreference(request.StatementPreference);
        application.StatementDay = request.StatementDay;
        application.ContactlessEnabled = request.ContactlessEnabled;
        application.InternetShoppingEnabled = request.InternetShoppingEnabled;
        application.AutomaticLimitIncreaseEnabled = request.AutomaticLimitIncreaseEnabled;
        application.AutomaticLimitIncreaseConsentAtUtc = request.AutomaticLimitIncreaseEnabled
            ? application.AutomaticLimitIncreaseConsentAtUtc ?? DateTime.UtcNow : null;
        application.IdentityDocumentConfirmed = request.IdentityDocumentConfirmed;
        application.IncomeDocumentConfirmed = request.IncomeDocumentConfirmed;
        application.ResidenceDocumentConfirmed = request.ResidenceDocumentConfirmed;
        application.ApplicationNote = request.ApplicationNote?.Trim();
        application.Status = ApplicationStatus.Pending;
        application.ApprovedLimit = null;
        application.EvaluationNote = null;
        application.EvaluatedByUserId = null;
        application.EvaluatedAtUtc = null;
        application.Histories.Add(new ApplicationHistory
        {
            PreviousStatus = previousStatus,
            NewStatus = ApplicationStatus.Pending,
            ChangedByUserId = officerUserId,
            Description = "Başvuru revizyon sonrası yeniden gönderildi."
        });
        if (activeRevision is not null)
            application.RevisionSnapshots.Add(new ApplicationRevisionSnapshot
            {
                RevisionNumber = activeRevision.RevisionNumber, Stage = "After",
                DataJson = JsonSerializer.Serialize(CreateSnapshot(application)),
                CapturedByUserId = officerUserId
            });
        await applicationRepository.SaveChangesAsync(cancellationToken);
        return Map(application, availableLimit);
    }

    private static void ValidateRequestedLimit(decimal requestedLimit, CardType cardType)
    {
        if (requestedLimit <= 0)
            throw new ArgumentException("Talep edilen limit sıfırdan büyük olmalıdır.");
        if (requestedLimit > 100_000_000m)
            throw new ArgumentException("Talep edilen limit izin verilen giriş aralığının dışındadır.");
        if (cardType.MinimumLimit.HasValue && requestedLimit < cardType.MinimumLimit.Value)
            throw new InvalidOperationException(
                $"{cardType.Name} kart için talep edilebilecek minimum limit {cardType.MinimumLimit.Value:N2} TL'dir.");
    }

    private static void ValidateApprovedLimit(decimal approvedLimit, decimal availableLimit, CardType cardType)
    {
        if (approvedLimit <= 0)
            throw new ArgumentException("Onaylanan limit sıfırdan büyük olmalıdır.");
        if (approvedLimit > availableLimit)
            throw new InvalidOperationException("Onaylanan limit müşterinin kullanılabilir azami limitini aşamaz.");
        if (cardType.MinimumLimit.HasValue && approvedLimit < cardType.MinimumLimit.Value)
            throw new InvalidOperationException($"{cardType.Name} kart için minimum limit {cardType.MinimumLimit.Value:N2} TL'dir.");
        if (cardType.MaximumLimit.HasValue && approvedLimit > cardType.MaximumLimit.Value)
            throw new InvalidOperationException($"{cardType.Name} kart için maksimum limit {cardType.MaximumLimit.Value:N2} TL'dir.");
    }

    private static CardTypeResponse MapCardType(CardType cardType)
    {
        var normalizedName = cardType.Name.ToLowerInvariant();
        var network = normalizedName.Contains("troy") ? "TROY"
            : normalizedName.Contains("mastercard") || normalizedName.Contains("world") ? "Mastercard" : "Visa";
        var productName = normalizedName.StartsWith("platinum plus") ? "Platinum Plus"
            : normalizedName.StartsWith("platinum") ? "Platinum"
            : normalizedName.StartsWith("gold") ? "Gold" : "Classic";
        var product = normalizedName switch
        {
            var name when name.StartsWith("classic") => ($"{network} ağı üzerinde günlük harcamalar için erişilebilir kart.", "Yıllık ücret: 689 TL", new[] { $"{network} iş yeri ağı", "Taksitli alışveriş", "Temassız ve mobil cüzdan desteği" }),
            var name when name.StartsWith("gold") => ($"{network} ağı ile alışveriş ve seyahat ayrıcalıkları.", "Yıllık ücret: 1.129 TL", new[] { "Artırılmış puan kazanımı", "Seçili restoran indirimleri", $"{network} kampanyaları" }),
            var name when name.StartsWith("platinum plus") => ("Üst segment yaşam tarzı ve seyahat ayrıcalıkları.", "Yıllık ücret: 2.499 TL", new[] { "Özel müşteri hattı", "Global lounge ağı", "Yüksek oranlı mil ve nakit iade" }),
            _ => ("Sık seyahat eden ve premium hizmet bekleyen müşteriler için Visa ürünü.", "Yıllık ücret: 1.799 TL", new[] { "Havalimanı lounge erişimi", "Mil/puan avantajı", "Seyahat sigortası" })
        };
        return new CardTypeResponse(cardType.Id, cardType.Name, productName, network, cardType.Bin,
            cardType.MinimumLimit, cardType.MaximumLimit,
            product.Item1, product.Item2, product.Item3);
    }

    private static CardApplicationResponse Map(
        CardApplication application,
        decimal? availableLimit = null,
        bool isEvaluationBlocked = false)
    {
        var calculatedAvailableLimit = availableLimit
            ?? Math.Max(0, application.Customer.MonthlyNetIncome * 3 - application.Customer.OtherBankTotalCardLimit);
        return new CardApplicationResponse(
            application.Id,
            application.ApplicationNumber,
            application.CustomerId,
            application.Customer.CustomerNumber,
            $"{application.Customer.FirstName} {application.Customer.LastName}",
            application.Customer.NationalIdentityNumber,
            application.Customer.PhoneCountryCode,
            application.Customer.PhoneNumber,
            application.Customer.EmailAddress,
            application.Customer.MonthlyNetIncome,
            application.Customer.OtherBankTotalCardLimit,
            application.Customer.CreditScore,
            application.CardTypeId,
            application.CardType.Name,
            application.RequestedLimit,
            calculatedAvailableLimit,
            application.DeliveryMethod,
            application.DeliveryAddress,
            application.DeliveryCity,
            application.DeliveryDistrict,
            application.DeliveryNeighborhood,
            application.DeliveryRecipientName,
            application.DeliveryPhone,
            application.DeliveryBranch,
            application.StatementPreference,
            application.StatementDay,
            application.ContactlessEnabled,
            application.InternetShoppingEnabled,
            application.AutomaticLimitIncreaseEnabled,
            application.IdentityDocumentConfirmed,
            application.IncomeDocumentConfirmed,
            application.ResidenceDocumentConfirmed,
            application.PreAssessmentScore,
            application.PreAssessmentRiskLevel,
            application.PreAssessmentRecommendation,
            application.ApplicationNote,
            application.Status.ToString(),
            application.CreatedAtUtc,
            isEvaluationBlocked,
            application.AssignedOfficerUserId,
            application.AssignedOfficerUser?.FullName,
            application.AssignedAtUtc,
            application.RequiresSecondApproval,
            WorkflowStage(application),
            application.FirstApprovedByUserId,
            application.FirstApprovedByUser?.FullName,
            application.FirstApprovedAtUtc,
            AutoAssignmentCompleted: application.AutoAssignmentCompleted,
            LastAssignmentReason: application.LastAssignmentReason,
            EscalationLevel: application.EscalationLevel,
            CancellationReason: application.CancellationReason,
            CancelledAtUtc: application.CancelledAtUtc,
            CancelledByName: application.CancelledByUser?.FullName);
    }

    private static string WorkflowStage(CardApplication application) => application.Status switch
    {
        ApplicationStatus.Approved => "Completed",
        ApplicationStatus.Rejected => "Completed",
        ApplicationStatus.Withdrawn => "Completed",
        ApplicationStatus.Cancelled => "Completed",
        ApplicationStatus.Revision => "OfficerRevision",
        _ when application.RequiresSecondApproval && application.FirstApprovedByUserId.HasValue => "SecondManagerApproval",
        _ => "ManagerReview"
    };

    private static string NormalizeStatementPreference(string value)
    {
        if (value.Equals("Email", StringComparison.OrdinalIgnoreCase)) return "Email";
        if (value.Equals("Paper", StringComparison.OrdinalIgnoreCase)) return "Paper";
        if (value.Equals("Mobile", StringComparison.OrdinalIgnoreCase)) return "Mobile";
        throw new ArgumentException("Ekstre tercihi Email, Paper veya Mobile olmalıdır.");
    }

    private static void ValidateApplicationNote(string? note)
    {
        if (note?.Trim().Length > 500)
            throw new ArgumentException("Başvuru notu en fazla 500 karakter olabilir.");
    }

    private static void ValidateStatementDay(int value)
    {
        if (value is not (7 or 14 or 21 or 28))
            throw new ArgumentException("Ekstre kesim günü 7, 14, 21 veya 28 olmalıdır.");
    }

    private static void ValidateDocuments(
        bool identityDocumentConfirmed,
        bool incomeDocumentConfirmed,
        bool residenceDocumentConfirmed)
    {
        var missing = new List<string>();
        if (!identityDocumentConfirmed) missing.Add("Kimlik");
        if (!incomeDocumentConfirmed) missing.Add("Gelir Belgesi");
        if (!residenceDocumentConfirmed) missing.Add("İkametgah");
        if (missing.Count > 0)
            throw new InvalidOperationException(
                $"Başvuru oluşturulmadan önce eksik belgeler tamamlanmalıdır: {string.Join(", ", missing)}.");
    }

    private static Dictionary<string, string> CreateSnapshot(CardApplication application) => new()
    {
        ["Kart tipi"] = application.CardType.Name,
        ["Aylık gelir"] = $"{application.Customer.MonthlyNetIncome:N2} TL",
        ["Talep edilen limit"] = $"{application.RequestedLimit:N2} TL",
        ["Teslimat yöntemi"] = application.DeliveryMethod,
        ["Teslimat adresi"] = application.DeliveryAddress,
        ["Ekstre tercihi"] = application.StatementPreference,
        ["Ekstre kesim günü"] = application.StatementDay.ToString(),
        ["Temassız kullanım"] = application.ContactlessEnabled ? "Açık" : "Kapalı",
        ["İnternet alışverişi"] = application.InternetShoppingEnabled ? "Açık" : "Kapalı"
        ,["Kimlik belgesi"] = application.IdentityDocumentConfirmed ? "Kontrol edildi" : "Eksik"
        ,["Gelir belgesi"] = application.IncomeDocumentConfirmed ? "Kontrol edildi" : "Eksik"
        ,["İkametgah"] = application.ResidenceDocumentConfirmed ? "Kontrol edildi" : "Eksik"
    };

    private static DeliverySelection ValidateDelivery(
        string deliveryMethod,
        string deliveryAddress,
        string? city,
        string? district,
        string? neighborhood,
        string? recipientName,
        string? phone,
        string? branch,
        Customer customer)
    {
        var method = deliveryMethod.Trim();
        if (method.Equals("RegisteredAddress", StringComparison.OrdinalIgnoreCase))
        {
            var requestedAddress = deliveryAddress.Trim();
            var activeAddresses = customer.SavedAddresses.Where(x => x.IsActive).ToList();
            var selectedAddress = activeAddresses.FirstOrDefault(x =>
                string.Equals(x.FullAddress.Trim(), requestedAddress, StringComparison.OrdinalIgnoreCase));
            if (activeAddresses.Count > 0 && selectedAddress is null)
                throw new ArgumentException("Teslimat için müşterinin aktif kayıtlı adreslerinden biri seçilmelidir.");
            var registeredAddress = selectedAddress?.FullAddress.Trim()
                ?? (!string.IsNullOrWhiteSpace(customer.Address) ? customer.Address.Trim() : requestedAddress);
            if (registeredAddress.Length < 10 || registeredAddress.Length > 500)
                throw new ArgumentException("Kayıtlı teslimat adresi 10-500 karakter arasında olmalıdır.");
            return new DeliverySelection("RegisteredAddress", registeredAddress,
                selectedAddress?.City ?? customer.City, selectedAddress?.District ?? customer.District,
                selectedAddress?.Neighborhood ?? customer.Neighborhood, $"{customer.FirstName} {customer.LastName}",
                $"{customer.PhoneCountryCode}{customer.PhoneNumber}", null);
        }

        if (method.Equals("DifferentAddress", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(city) || string.IsNullOrWhiteSpace(district)
                || string.IsNullOrWhiteSpace(neighborhood)
                || string.IsNullOrWhiteSpace(recipientName))
                throw new ArgumentException("Farklı adres teslimatı için il, ilçe, mahalle ve teslim alacak kişi zorunludur.");
            var normalizedPhone = NormalizeDeliveryPhone(phone);
            var address = deliveryAddress.Trim();
            if (address.Length < 10 || address.Length > 500)
                throw new ArgumentException("Kart teslimat adresi 10-500 karakter arasında olmalıdır.");
            return new DeliverySelection("DifferentAddress", address, city.Trim(), district.Trim(),
                neighborhood?.Trim(), recipientName.Trim(), normalizedPhone, null);
        }

        if (method.Equals("Branch", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(branch))
                throw new ArgumentException("Teslimat şubesi seçilmelidir.");
            return new DeliverySelection("Branch", branch.Trim(), null, null, null,
                $"{customer.FirstName} {customer.LastName}",
                $"{customer.PhoneCountryCode}{customer.PhoneNumber}", branch.Trim());
        }

        throw new ArgumentException("Teslimat yöntemi RegisteredAddress, DifferentAddress veya Branch olmalıdır.");
    }

    private static string NormalizeDeliveryPhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Teslimat telefonu zorunludur.");
        var trimmed = value.Trim();
        var digits = new string(trimmed.Where(char.IsDigit).ToArray());
        if (trimmed.StartsWith('+') && digits.Length is >= 8 and <= 15)
            return $"+{digits}";
        if (digits.Length == 11 && digits.StartsWith('0'))
            return $"+90{digits[1..]}";
        throw new ArgumentException("Teslimat telefonu ülke kodu ile geçerli formatta olmalıdır.");
    }

    private sealed record DeliverySelection(
        string Method,
        string Address,
        string? City,
        string? District,
        string? Neighborhood,
        string? RecipientName,
        string? Phone,
        string? Branch);

    private static CreditCard CreateDemoCard(CardApplication application, decimal approvedLimit)
    {
        var pan = PaymentCardNumberGenerator.Generate(application.CardType.Bin);
        return new CreditCard
        {
            MaskedCardNumber = PaymentCardNumberGenerator.Mask(pan, application.CardType.Bin.Length),
            CardLimit = approvedLimit,
            Status = CardStatus.Inactive,
            IssueDate = DateOnly.FromDateTime(DateTime.UtcNow),
            ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(4))
        };
    }
}
