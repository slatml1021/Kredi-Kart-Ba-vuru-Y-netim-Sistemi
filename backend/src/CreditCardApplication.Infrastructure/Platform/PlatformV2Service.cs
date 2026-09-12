using System.Net;
using System.Net.Mail;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using CreditCardApplication.Application.Applications;
using CreditCardApplication.Application.Customers;
using CreditCardApplication.Application.Platform;
using CreditCardApplication.Application.Services;
using CreditCardApplication.Domain.Entities;
using CreditCardApplication.Domain.Enums;
using CreditCardApplication.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CreditCardApplication.Infrastructure.Platform;

public sealed class PlatformV2Service(
    ApplicationDbContext db,
    IConfiguration configuration,
    LimitCalculator limitCalculator) : IPlatformV2Service
{
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> SupplementaryCardLocks = new();
    private const string DecisionSupportDisclaimer =
        "Bu sonuç açıklanabilir bir karar destek simülasyonudur; otomatik onay veya ret kararı vermez.";

    static PlatformV2Service() => QuestPDF.Settings.License = LicenseType.Community;

    private static readonly HashSet<string> UploadDocumentTypes =
        new(StringComparer.OrdinalIgnoreCase) { "Identity", "Income", "Residence", "Other" };

    public async Task<KkbRiskAnalysisResponse> GetKkbAnalysisAsync(
        int customerId, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.AsNoTracking()
            .Include(x => x.OtherBankCards)
            .FirstOrDefaultAsync(x => x.Id == customerId, cancellationToken)
            ?? throw new ArgumentException("Müşteri bulunamadı.");
        var (category, risk) = customer.CreditScore switch
        {
            <= 969 => ("Kritik", "VERY_HIGH"),
            <= 1149 => ("Gelişime Açık", "HIGH"),
            <= 1469 => ("Dengeli", "MEDIUM"),
            <= 1719 => ("Güvenli", "LOW"),
            _ => ("Prestijli", "VERY_LOW")
        };
        var factors = new List<string>
        {
            $"Findeks kredi notu simülasyonu: {customer.CreditScore}/1900",
            $"Diğer bankalardaki aktif kart limiti: {customer.OtherBankCards.Where(x => x.IsActive).Sum(x => x.CardLimit):N2} TL",
            $"Aylık net gelir: {customer.MonthlyNetIncome:N2} TL"
        };
        if (!customer.IsPhoneVerified || !customer.IsEmailVerified)
            factors.Add("İletişim doğrulamalarından en az biri eksik.");
        return new(customer.Id, customer.CreditScore, category, risk, "SIMULATION",
            $"{category} kategorisi resmi Findeks puan aralıklarına göre eşlenmiştir. Gerçek KKB sorgusu değildir.",
            factors, DateTime.UtcNow);
    }

    public async Task<PreAssessmentResponse> GetPreAssessmentAsync(
        int applicationId, CancellationToken cancellationToken)
    {
        var application = await db.CardApplications
            .Include(x => x.Customer).ThenInclude(x => x.OtherBankCards)
            .Include(x => x.CardType)
            .FirstOrDefaultAsync(x => x.Id == applicationId, cancellationToken)
            ?? throw new ArgumentException("Başvuru bulunamadı.");
        var ownBankLimits = await db.CreditCards.AsNoTracking()
            .Where(x => x.CardApplication.CustomerId == application.CustomerId
                        && x.CardApplication.Status == ApplicationStatus.Approved)
            .Select(x => x.CardLimit).ToListAsync(cancellationToken);
        var ownBankLimit = ownBankLimits.Sum();
        var availableLimit = limitCalculator.Calculate(
            application.Customer.MonthlyNetIncome,
            application.Customer.OtherBankTotalCardLimit,
            ownBankLimit).AvailableLimit;
        var result = CalculatePreAssessment(application.Customer, application.CardType.Name,
            application.RequestedLimit, availableLimit);
        application.PreAssessmentScore = result.Score;
        application.PreAssessmentRiskLevel = result.RiskLevel;
        application.PreAssessmentRecommendation = result.Recommendation;
        application.PreAssessmentPositiveFactorsJson = JsonSerializer.Serialize(result.PositiveFactors);
        application.PreAssessmentRiskFactorsJson = JsonSerializer.Serialize(result.RiskFactors);
        application.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return result;
    }

    public async Task<DuplicateApplicationResponse> CheckDuplicateAsync(
        int customerId, int cardTypeId, CancellationToken cancellationToken)
    {
        // Başvuru oluşturma servisindeki kuralla aynı sonucu üret: onaylanmış ve
        // kartı basılmış aynı ürün için tarih sınırı yoktur.
        var approved = await db.CardApplications.AsNoTracking()
            .Where(x => x.CustomerId == customerId && x.CardTypeId == cardTypeId
                && x.Status == ApplicationStatus.Approved && x.CreditCard != null)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (approved is not null)
        {
            return new(true,
                $"Bu müşterinin aynı tipte aktif/onaylı kartı vardır: {approved.ApplicationNumber}.",
                approved.Id, approved.ApplicationNumber, approved.Status.ToString(), approved.CreatedAtUtc);
        }

        var threshold = DateTime.UtcNow.AddDays(-15);
        var duplicate = await db.CardApplications.AsNoTracking()
            .Where(x => x.CustomerId == customerId && x.CardTypeId == cardTypeId && x.CreatedAtUtc >= threshold
                && (x.Status == ApplicationStatus.Pending || x.Status == ApplicationStatus.Revision))
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        return duplicate is null
            ? new(false, "Son 15 gün içinde aynı kart tipi için başvuru bulunamadı.", null, null, null, null)
            : new(true,
                $"Bu müşterinin aynı kart tipi için değerlendirmedeki kaydı vardır: {duplicate.ApplicationNumber}.",
                duplicate.Id, duplicate.ApplicationNumber, duplicate.Status.ToString(), duplicate.CreatedAtUtc);
    }

    public async Task<SupplementaryApplicationResponse> CreateSupplementaryAsync(
        CreateSupplementaryApplicationRequest request, int userId, CancellationToken cancellationToken)
    {
        var gate = SupplementaryCardLocks.GetOrAdd(request.PrimaryCreditCardId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
        var card = await db.CreditCards
            .Include(x => x.CardApplication).ThenInclude(x => x.Customer).ThenInclude(x => x.SavedAddresses)
            .FirstOrDefaultAsync(x => x.Id == request.PrimaryCreditCardId, cancellationToken)
            ?? throw new ArgumentException("Ana kredi kartı bulunamadı.");
        if (!card.CardApplication.Customer.IsActive)
            throw new InvalidOperationException("Pasif ana kart müşterisi için ek kart başvurusu oluşturulamaz.");
        if (card.Status != CardStatus.Active)
            throw new InvalidOperationException("Yalnızca aktif bir ana kart için ek kart başvurusu oluşturulabilir.");
        if (card.CardLimit <= 0)
            throw new InvalidOperationException("Kullanılabilir ana kart limiti bulunmadığı için ek kart başvurusu oluşturulamaz.");
        var holder = await db.Customers.FirstOrDefaultAsync(
            x => x.Id == request.SupplementaryHolderCustomerId, cancellationToken)
            ?? throw new ArgumentException("Ek kart sahibi müşteri bulunamadı.");
        if (card.CardApplication.CustomerId == holder.Id)
            throw new InvalidOperationException("Kişi kendisi adına ek kart çıkaramaz.");
        if (!holder.IsActive)
            throw new InvalidOperationException("Pasif müşteri adına ek kart başvurusu oluşturulamaz.");
        if (!holder.BirthDate.HasValue || holder.BirthDate.Value > DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-18))
            throw new InvalidOperationException("Ek kart sahibi 18 yaşını doldurmuş olmalıdır.");
        var missing = CustomerService.GetMissingProfileFields(holder);
        if (missing.Count > 0)
            throw new InvalidOperationException($"Ek kart sahibinin bilgileri tamamlanmalıdır: {string.Join(", ", missing)}.");
        var allowedRelationships = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "Eş", "Anne", "Baba", "Çocuk", "Kardeş", "Diğer" };
        var relationship = request.Relationship.Trim();
        if (!allowedRelationships.Contains(relationship))
            throw new ArgumentException("Geçerli bir yakınlık derecesi seçilmelidir.");
        ValidateSupplementaryRelationshipAge(card.CardApplication.Customer, holder, relationship);
        var duplicate = await db.SupplementaryCardApplications.AnyAsync(
            x => x.PrimaryCreditCardId == card.Id && x.SupplementaryHolderCustomerId == holder.Id
                 && (x.Status == "Pending" || x.Status == "Approved"), cancellationToken);
        if (duplicate)
            throw new InvalidOperationException("Bu ana kart ve ek kart sahibi için açık/onaylı bir ek kart kaydı bulunmaktadır.");
        var reservedAllocations = await db.SupplementaryCardApplications.AsNoTracking()
            .Where(x => x.PrimaryCreditCardId == card.Id
                        && (x.Status == "Pending" || x.Status == "Approved"))
            .Select(x => x.RequestedLimit)
            .ToListAsync(cancellationToken);
        var reservedLimit = reservedAllocations.Sum();
        var availableLimit = Math.Max(0, card.CardLimit - reservedLimit);
        if (request.RequestedLimit <= 0)
            throw new ArgumentException("Ek kart limiti sıfırdan büyük olmalıdır.");
        if (request.RequestedLimit > availableLimit)
            throw new InvalidOperationException(
                $"Girilen limit kullanılabilir limiti aşamaz. En fazla {availableLimit:N2} TL girilebilir.");

        var deliveryMethod = request.DeliveryMethod.Trim();
        if (deliveryMethod is not ("RegisteredAddress" or "Branch" or "DifferentAddress"))
            throw new ArgumentException("Geçerli bir teslimat yöntemi seçilmelidir.");
        var deliveryAddress = request.DeliveryAddress?.Trim();
        if (deliveryMethod == "RegisteredAddress")
        {
            var customer = card.CardApplication.Customer;
            var activeAddresses = customer.SavedAddresses.Where(x => x.IsActive).ToList();
            if (string.IsNullOrWhiteSpace(deliveryAddress))
                deliveryAddress = activeAddresses.FirstOrDefault(x => x.IsDefault)?.FullAddress
                    ?? activeAddresses.FirstOrDefault()?.FullAddress ?? customer.Address;
            if (activeAddresses.Count > 0 && !activeAddresses.Any(x =>
                    string.Equals(x.FullAddress.Trim(), deliveryAddress, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Teslimat için ana kart sahibinin aktif kayıtlı adreslerinden biri seçilmelidir.");
        }
        if (string.IsNullOrWhiteSpace(deliveryAddress) || deliveryAddress.Length < 5)
            throw new ArgumentException(deliveryMethod == "Branch"
                ? "Teslimat şubesi seçilmelidir."
                : "Teslimat adresi en az 5 karakter olmalıdır.");

        var entity = new SupplementaryCardApplication
        {
            ApplicationNumber = $"EKK-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            PrimaryCustomerId = card.CardApplication.CustomerId,
            PrimaryCreditCardId = card.Id,
            SupplementaryHolderCustomerId = holder.Id,
            Relationship = relationship,
            RequestedLimit = request.RequestedLimit,
            DeliveryMethod = deliveryMethod,
            DeliveryAddress = deliveryAddress,
            CreatedByUserId = userId
        };
        db.SupplementaryCardApplications.Add(entity);
        await NotifyRoleAsync("Manager", "Ek kart başvurusu",
            $"{entity.ApplicationNumber} numaralı ek kart başvurusu değerlendirme bekliyor.",
            "/manager/supplementary-applications", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        entity.PrimaryCustomer = card.CardApplication.Customer;
        entity.SupplementaryHolderCustomer = holder;
        return Map(entity);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<SupplementaryCardContextResponse> GetSupplementaryCardContextAsync(
        int primaryCardId, int userId, CancellationToken cancellationToken)
    {
        var card = await db.CreditCards.AsNoTracking()
            .Include(x => x.CardApplication).ThenInclude(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Id == primaryCardId, cancellationToken)
            ?? throw new ArgumentException("Ana kredi kartı bulunamadı.");
        if (card.Status != CardStatus.Active)
            throw new InvalidOperationException("Yalnızca aktif bir ana kart ek kart başvurusuna bağlanabilir.");
        var allocations = await db.SupplementaryCardApplications.AsNoTracking()
            .Where(x => x.PrimaryCreditCardId == card.Id
                        && (x.Status == "Pending" || x.Status == "Approved"))
            .Select(x => new { x.Status, x.RequestedLimit })
            .ToListAsync(cancellationToken);
        var used = allocations.Where(x => x.Status == "Approved").Sum(x => x.RequestedLimit);
        var reserved = allocations.Sum(x => x.RequestedLimit);
        var customer = card.CardApplication.Customer;
        return new(
            card.Id, card.MaskedCardNumber, card.Status.ToString(), card.CardLimit, used, reserved,
            Math.Max(0, card.CardLimit - reserved),
            customer.Id, customer.CustomerNumber,
            $"{customer.FirstName} {customer.LastName}", customer.NationalIdentityNumber,
            customer.IsActive, customer.BirthDate, customer.Address);
    }

    public async Task<IReadOnlyList<SupplementaryApplicationResponse>> GetSupplementaryAsync(
        bool all, int userId, CancellationToken cancellationToken)
    {
        var query = db.SupplementaryCardApplications.AsNoTracking()
            .Include(x => x.PrimaryCustomer).Include(x => x.SupplementaryHolderCustomer).AsQueryable();
        if (!all) query = query.Where(x => x.CreatedByUserId == userId);
        return (await query.OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken))
            .Select(Map).ToList();
    }

    public async Task<SupplementaryApplicationResponse> GetSupplementaryByIdAsync(
        int id, bool isManager, int userId, CancellationToken cancellationToken)
    {
        var query = db.SupplementaryCardApplications.AsNoTracking()
            .Include(x => x.PrimaryCustomer).Include(x => x.SupplementaryHolderCustomer).AsQueryable();
        if (!isManager) query = query.Where(x => x.CreatedByUserId == userId);
        var entity = await query.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Ek kart başvurusu bulunamadı veya bu kaydı görüntüleme yetkiniz yok.");
        return Map(entity);
    }

    public async Task<SupplementaryApplicationResponse> EvaluateSupplementaryAsync(
        int id, EvaluateSupplementaryApplicationRequest request, int userId, CancellationToken cancellationToken)
    {
        var cardId = await db.SupplementaryCardApplications.AsNoTracking()
            .Where(x => x.Id == id).Select(x => x.PrimaryCreditCardId)
            .FirstOrDefaultAsync(cancellationToken);
        if (cardId == 0) throw new ArgumentException("Ek kart başvurusu bulunamadı.");
        var gate = SupplementaryCardLocks.GetOrAdd(cardId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
        var entity = await db.SupplementaryCardApplications
            .Include(x => x.PrimaryCustomer).Include(x => x.SupplementaryHolderCustomer)
            .Include(x => x.PrimaryCreditCard)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ArgumentException("Ek kart başvurusu bulunamadı.");
        if (entity.Status != "Pending")
            throw new InvalidOperationException("Yalnızca bekleyen ek kart başvuruları değerlendirilebilir.");
        var decision = request.Decision.Trim();
        if (decision is not ("Approved" or "Rejected"))
            throw new ArgumentException("Karar Approved veya Rejected olmalıdır.");
        if (decision == "Rejected" && string.IsNullOrWhiteSpace(request.Note))
            throw new ArgumentException("Ret nedeni zorunludur.");
        entity.Status = decision;
        if (decision == "Approved")
        {
            if (!entity.PrimaryCustomer.IsActive || !entity.SupplementaryHolderCustomer.IsActive)
                throw new InvalidOperationException("Pasif müşteri bulunan ek kart başvurusu onaylanamaz.");
            var otherReservedLimits = await db.SupplementaryCardApplications.AsNoTracking()
                .Where(x => x.PrimaryCreditCardId == entity.PrimaryCreditCardId && x.Id != entity.Id
                            && (x.Status == "Pending" || x.Status == "Approved"))
                .Select(x => x.RequestedLimit).ToListAsync(cancellationToken);
            var otherReserved = otherReservedLimits.Sum();
            if (otherReserved + entity.RequestedLimit > entity.PrimaryCreditCard.CardLimit)
                throw new InvalidOperationException("Güncel ana kart limiti ek kart talebini karşılamıyor.");
            entity.MaskedCardNumber = $"EK **** **** {Random.Shared.Next(0, 10_000):D4}";
            entity.IssuedAtUtc = DateTime.UtcNow;
            entity.CardStatus = "Inactive";
            entity.FulfillmentStatus = "Production";
            entity.EstimatedPrintAtUtc = DateTime.UtcNow.AddHours(4);
            entity.EstimatedDeliveryAtUtc = DateTime.UtcNow.AddDays(3);
            entity.NextTransitionAtUtc = DateTime.UtcNow.AddHours(2);
        }
        entity.EvaluationNote = request.Note?.Trim();
        entity.EvaluatedByUserId = userId;
        entity.EvaluatedAtUtc = DateTime.UtcNow;
        await NotifyUserAsync(entity.CreatedByUserId, "Ek kart başvurusu sonuçlandı",
            $"{entity.ApplicationNumber} numaralı ek kart başvurusu: {TurkishStatus(decision)}.",
            "/officer/supplementary-applications", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Map(entity);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<FulfillmentResponse?> GetFulfillmentAsync(
        int creditCardId, CancellationToken cancellationToken)
    {
        var card = await db.CreditCards.Include(x => x.Fulfillment)
            .FirstOrDefaultAsync(x => x.Id == creditCardId, cancellationToken);
        if (card is null) return null;
        var fulfillment = card.Fulfillment ?? CreateFulfillment(card);
        AdvanceFulfillment(fulfillment, DateTime.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return Map(fulfillment);
    }

    public async Task<SlaDashboardResponse> GetSlaAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var applications = await db.CardApplications.AsNoTracking()
            .Include(x => x.Customer).Include(x => x.Histories).ToListAsync(cancellationToken);
        var evaluated = applications.Where(x => x.EvaluatedAtUtc.HasValue).ToList();
        var average = evaluated.Count == 0 ? 0
            : evaluated.Average(x => (x.EvaluatedAtUtc!.Value - x.CreatedAtUtc).TotalMinutes);
        var open = applications.Where(x => x.Status is ApplicationStatus.Pending or ApplicationStatus.Revision)
            .Select(x => ToSlaItem(x, now)).OrderByDescending(x => x.WaitingHours).ToList();
        var revisions = applications.Count(x => x.Histories.Any(h => h.NewStatus == ApplicationStatus.Revision));
        var revisionReturned = applications.Count(x =>
            x.Histories.Any(h => h.NewStatus == ApplicationStatus.Revision)
            && x.Histories.Any(h => h.PreviousStatus == ApplicationStatus.Revision && h.NewStatus == ApplicationStatus.Pending));
        return new(Math.Round(average, 1), open.Count(x => x.WaitingHours > 8),
            Math.Round(open.FirstOrDefault()?.WaitingHours ?? 0, 1),
            revisions == 0 ? 0 : Math.Round(100m * revisionReturned / revisions, 1), open.Take(5).ToList());
    }

    public async Task<IReadOnlyList<NotificationResponse>> GetNotificationsAsync(
        int userId, CancellationToken cancellationToken) =>
        (await db.Notifications.AsNoTracking().Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc).Take(50).ToListAsync(cancellationToken))
        .Select(x => new NotificationResponse(x.Id, x.Type, x.Title, x.Message, x.Link, x.IsRead, x.CreatedAtUtc)).ToList();

    public async Task MarkNotificationReadAsync(int notificationId, int userId, CancellationToken cancellationToken)
    {
        var notification = await db.Notifications.FirstOrDefaultAsync(
            x => x.Id == notificationId && x.UserId == userId, cancellationToken)
            ?? throw new ArgumentException("Bildirim bulunamadı.");
        notification.IsRead = true;
        notification.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkAllNotificationsReadAsync(int userId, CancellationToken cancellationToken)
    {
        var unread = await db.Notifications
            .Where(x => x.UserId == userId && !x.IsRead)
            .ToListAsync(cancellationToken);
        foreach (var notification in unread)
        {
            notification.IsRead = true;
            notification.UpdatedAtUtc = DateTime.UtcNow;
        }
        if (unread.Count > 0) await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ApplicationDocumentResponse> UploadApplicationDocumentAsync(
        int applicationId, string documentType, string fileName, byte[] content, int userId,
        CancellationToken cancellationToken)
    {
        var application = await db.CardApplications.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == applicationId, cancellationToken)
            ?? throw new ArgumentException("Başvuru bulunamadı.");
        var isManager = await db.Users.AsNoTracking().AnyAsync(x => x.Id == userId
            && x.UserRoles.Any(role => role.Role.Name == "Manager"), cancellationToken);
        if (!isManager && application.CreatedByUserId != userId)
            throw new UnauthorizedAccessException("Bu başvuruya belge yükleme yetkiniz yok.");
        if (!UploadDocumentTypes.Contains(documentType))
            throw new ArgumentException("Belge türü Identity, Income, Residence veya Other olmalıdır.");
        if (content.Length is 0 or > 5 * 1024 * 1024)
            throw new ArgumentException("Belge boyutu 1 bayt ile 5 MB arasında olmalıdır.");
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension is not (".pdf" or ".jpg" or ".jpeg" or ".png"))
            throw new ArgumentException("Yalnızca PDF, JPG ve PNG belgeleri yüklenebilir.");
        var signatureValid = extension switch
        {
            ".pdf" => content.Length >= 4 && content.AsSpan(0, 4).SequenceEqual("%PDF"u8),
            ".png" => content.Length >= 8 && content.AsSpan(0, 8).SequenceEqual(
                new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            _ => content.Length >= 3 && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF
        };
        if (!signatureValid)
            throw new ArgumentException("Belge içeriği dosya uzantısıyla uyuşmuyor.");
        var safeFileName = Path.GetFileNameWithoutExtension(fileName);
        safeFileName = string.Concat(safeFileName.Where(character => char.IsLetterOrDigit(character) || character is '-' or '_'));
        if (string.IsNullOrWhiteSpace(safeFileName)) safeFileName = "belge";
        var storageDirectory = Path.Combine(AppContext.BaseDirectory, "App_Data", "uploads", $"application-{applicationId}");
        Directory.CreateDirectory(storageDirectory);
        var storedName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{documentType}-{safeFileName}{extension}";
        var storagePath = Path.Combine(storageDirectory, storedName);
        await File.WriteAllBytesAsync(storagePath, content, cancellationToken);
        var document = new GeneratedDocument
        {
            EntityType = "CardApplicationUpload", EntityId = applicationId, DocumentType = documentType,
            FileName = Path.GetFileName(fileName), StoragePath = storagePath,
            Sha256 = Convert.ToHexString(SHA256.HashData(content)), EmailStatus = "NotApplicable",
            VerificationStatus = "PendingVerification"
        };
        db.GeneratedDocuments.Add(document);
        await db.SaveChangesAsync(cancellationToken);
        return new(document.Id, applicationId, document.DocumentType, document.FileName, document.Sha256,
            document.CreatedAtUtc, document.VerificationStatus, null, null, null, null);
    }

    public async Task<IReadOnlyList<ApplicationDocumentResponse>> GetApplicationDocumentsAsync(
        int applicationId, int userId, CancellationToken cancellationToken)
    {
        await EnsureApplicationDocumentAccessAsync(applicationId, userId, cancellationToken);
        return await db.GeneratedDocuments.AsNoTracking()
            .Include(x => x.VerifiedByUser)
            .Where(x => x.EntityType == "CardApplicationUpload" && x.EntityId == applicationId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new ApplicationDocumentResponse(x.Id, applicationId, x.DocumentType,
                x.FileName, x.Sha256, x.CreatedAtUtc, x.VerificationStatus,
                x.VerifiedByUser == null ? null : x.VerifiedByUser.FullName,
                x.VerifiedAtUtc, x.VerificationNote, x.ExpiresAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<StoredDocumentResponse> GetApplicationDocumentAsync(
        int applicationId, int documentId, int userId, CancellationToken cancellationToken)
    {
        await EnsureApplicationDocumentAccessAsync(applicationId, userId, cancellationToken);
        var document = await db.GeneratedDocuments.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Id == documentId && x.EntityType == "CardApplicationUpload" && x.EntityId == applicationId,
            cancellationToken) ?? throw new ArgumentException("Belge bulunamadı.");
        if (!File.Exists(document.StoragePath)) throw new FileNotFoundException("Belge dosyası depoda bulunamadı.");
        var extension = Path.GetExtension(document.FileName).ToLowerInvariant();
        var contentType = extension switch { ".pdf" => "application/pdf", ".png" => "image/png", _ => "image/jpeg" };
        return new(await File.ReadAllBytesAsync(document.StoragePath, cancellationToken), document.FileName, contentType);
    }

    private async Task EnsureApplicationDocumentAccessAsync(int applicationId, int userId, CancellationToken cancellationToken)
    {
        var canAccess = await db.CardApplications.AsNoTracking().AnyAsync(x => x.Id == applicationId
            && (x.CreatedByUserId == userId || db.Users.Any(user => user.Id == userId
                && user.UserRoles.Any(role => role.Role.Name == "Manager"))), cancellationToken);
        if (!canAccess) throw new UnauthorizedAccessException("Başvuru belgelerine erişim yetkiniz yok.");
    }

    public async Task NotifyLimitDecreaseAsync(
        int cardId, int officerUserId, decimal newLimit, CancellationToken cancellationToken)
    {
        db.Notifications.Add(NewNotification(
            officerUserId,
            "Limit azaltımı uygulandı",
            $"Kartın yeni limiti {newLimit:N2} TL olarak otomatik güncellendi.",
            $"/officer/cards/{cardId}"));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ChatUserResponse>> GetChatUsersAsync(
        int userId, CancellationToken cancellationToken)
    {
        var users = await db.Users.AsNoTracking().Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .Where(x => x.Id != userId && x.IsActive).OrderBy(x => x.FullName).ToListAsync(cancellationToken);
        return users.Select(x => new ChatUserResponse(x.Id, x.FullName,
            x.UserRoles.Select(r => r.Role.Name).FirstOrDefault() ?? "Personel")).ToList();
    }

    public async Task<IReadOnlyList<ChatMessageResponse>> GetConversationAsync(
        int userId, int otherUserId, CancellationToken cancellationToken)
    {
        var messages = await db.ChatMessages
            .Include(x => x.SenderUser).Include(x => x.RecipientUser)
            .Where(x => (x.SenderUserId == userId && x.RecipientUserId == otherUserId)
                     || (x.SenderUserId == otherUserId && x.RecipientUserId == userId))
            .OrderBy(x => x.CreatedAtUtc).Take(200).ToListAsync(cancellationToken);
        foreach (var message in messages.Where(x => x.RecipientUserId == userId && !x.IsRead))
        {
            message.IsRead = true;
            message.ReadAtUtc = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(cancellationToken);
        return messages.Select(Map).ToList();
    }

    public async Task<ChatMessageResponse> SendMessageAsync(
        int userId, SendChatMessageRequest request, CancellationToken cancellationToken)
    {
        ValidateChatMessage(request.Message);
        await ValidateChatApplicationReferenceAsync(request.ApplicationId, cancellationToken);
        await ValidateChatRateAsync(userId, request.Message, cancellationToken);
        var sender = await db.Users.FindAsync([userId], cancellationToken)
            ?? throw new UnauthorizedAccessException("Gönderen kullanıcı bulunamadı.");
        var recipient = await db.Users.Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(x => x.Id == request.RecipientUserId, cancellationToken)
            ?? throw new ArgumentException("Alıcı kullanıcı bulunamadı.");
        if (recipient.Id == sender.Id) throw new InvalidOperationException("Kendinize mesaj gönderemezsiniz.");
        var entity = new ChatMessage
        {
            SenderUserId = sender.Id, SenderUser = sender,
            RecipientUserId = recipient.Id, RecipientUser = recipient,
            CardApplicationId = request.ApplicationId, Message = request.Message.Trim()
        };
        db.ChatMessages.Add(entity);
        await NotifyUserAsync(recipient.Id, "Yeni mesaj", $"{sender.FullName}: {entity.Message}",
            $"/{RolePath(recipient)}/chat?user={sender.Id}", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<IReadOnlyList<ChatMessageResponse>> SendBulkMessageAsync(
        int userId, SendBulkChatMessageRequest request, CancellationToken cancellationToken)
    {
        ValidateChatMessage(request.Message);
        await ValidateChatApplicationReferenceAsync(request.ApplicationId, cancellationToken);
        await ValidateChatRateAsync(userId, request.Message, cancellationToken);
        var sender = await db.Users.FindAsync([userId], cancellationToken)
            ?? throw new UnauthorizedAccessException("Gönderen kullanıcı bulunamadı.");
        var primaryIds = request.RecipientUserIds.Where(x => x != userId).Distinct().ToHashSet();
        var ccIds = request.CcRecipientUserIds.Where(x => x != userId && !primaryIds.Contains(x)).Distinct().ToHashSet();
        var targetIds = primaryIds.Concat(ccIds).ToHashSet();
        if (targetIds.Count == 0) throw new ArgumentException("En az bir geçerli alıcı seçilmelidir.");
        var recipients = await db.Users.Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .Where(x => targetIds.Contains(x.Id) && x.IsActive).ToListAsync(cancellationToken);
        if (recipients.Count != targetIds.Count) throw new ArgumentException("Alıcılardan biri bulunamadı veya aktif değil.");
        var entities = recipients.Select(recipient => new ChatMessage
        {
            SenderUserId = sender.Id, SenderUser = sender,
            RecipientUserId = recipient.Id, RecipientUser = recipient,
            CardApplicationId = request.ApplicationId,
            Message = ccIds.Contains(recipient.Id) ? $"[BİLGİ/CC] {request.Message.Trim()}" : request.Message.Trim()
        }).ToList();
        db.ChatMessages.AddRange(entities);
        foreach (var entity in entities)
            await NotifyUserAsync(entity.RecipientUserId, "Yeni toplu mesaj", $"{sender.FullName}: {entity.Message}",
                $"/{RolePath(entity.RecipientUser)}/chat?user={sender.Id}", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return entities.Select(Map).ToList();
    }

    public async Task ReportChatMessageAsync(
        int userId, int messageId, ReportChatMessageRequest request, CancellationToken cancellationToken)
    {
        var allowedReasons = new[] { "Spam", "Uygunsuz İçerik", "Şüpheli Bağlantı", "Diğer" };
        var reason = request.Reason.Trim();
        if (!allowedReasons.Contains(reason, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Bildirim nedeni Spam, Uygunsuz İçerik, Şüpheli Bağlantı veya Diğer olmalıdır.");
        if (request.Note?.Trim().Length > 300)
            throw new ArgumentException("Bildirim açıklaması en fazla 300 karakter olabilir.");
        var message = await db.ChatMessages.AsNoTracking().Include(x => x.SenderUser)
            .FirstOrDefaultAsync(x => x.Id == messageId, cancellationToken)
            ?? throw new ArgumentException("Bildirilecek mesaj bulunamadı.");
        if (message.RecipientUserId != userId)
            throw new UnauthorizedAccessException("Yalnızca size gönderilen bir mesajı bildirebilirsiniz.");
        var alreadyReported = await db.AuditLogs.AnyAsync(x => x.UserId == userId
            && x.Action == "ChatMessageReported" && x.EntityId == messageId.ToString(), cancellationToken);
        if (alreadyReported) throw new InvalidOperationException("Bu mesaj daha önce bildirildi.");
        db.AuditLogs.Add(new AuditLog
        {
            UserId = userId, Action = "ChatMessageReported", EntityName = "ChatMessage",
            EntityId = messageId.ToString(),
            Detail = $"Neden: {reason}. Açıklama: {request.Note?.Trim() ?? "—"}. Gönderen: {message.SenderUser.FullName}"
        });
        await NotifyRoleAsync("Manager", "İletişim içeriği bildirildi",
            $"{message.SenderUser.FullName} tarafından gönderilen mesaj '{reason}' nedeniyle bildirildi.",
            "/manager/operations", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ConsentResponse>> GetConsentsAsync(
        int customerId, CancellationToken cancellationToken) =>
        (await db.CustomerConsents.AsNoTracking().Include(x => x.CapturedByUser)
            .Where(x => x.CustomerId == customerId).OrderByDescending(x => x.CapturedAtUtc)
            .ToListAsync(cancellationToken)).Select(Map).ToList();

    public async Task<ConsentResponse> SetConsentAsync(
        int customerId, ConsentRequest request, int userId, CancellationToken cancellationToken)
    {
        if (!await db.Customers.AnyAsync(x => x.Id == customerId, cancellationToken))
            throw new ArgumentException("Müşteri bulunamadı.");
        var type = request.ConsentType.Trim().ToUpperInvariant();
        if (type is not ("KVKK" or "SMS" or "EMAIL"))
            throw new ArgumentException("Onay türü KVKK, SMS veya EMAIL olmalıdır.");
        if (string.IsNullOrWhiteSpace(request.TextVersion) || request.TextVersion.Trim().Length > 30)
            throw new ArgumentException("Onay metni sürümü 1-30 karakter arasında olmalıdır.");
        var channel = request.Channel.Trim();
        if (channel is not ("Şube" or "Mobil" or "Web" or "Çağrı Merkezi"))
            throw new ArgumentException("Onay kanalı Şube, Mobil, Web veya Çağrı Merkezi olmalıdır.");
        var entity = new CustomerConsent
        {
            CustomerId = customerId, ConsentType = type, TextVersion = request.TextVersion.Trim(),
            IsGranted = request.IsGranted, Channel = channel, CapturedByUserId = userId,
            CapturedAtUtc = DateTime.UtcNow, WithdrawnAtUtc = request.IsGranted ? null : DateTime.UtcNow
        };
        db.CustomerConsents.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        entity.CapturedByUser = await db.Users.FindAsync([userId], cancellationToken) ?? new User { FullName = "Personel" };
        return Map(entity);
    }

    public async Task<RevisionComparisonResponse?> GetRevisionComparisonAsync(
        int applicationId, CancellationToken cancellationToken)
    {
        var snapshots = await db.ApplicationRevisionSnapshots.AsNoTracking()
            .Where(x => x.CardApplicationId == applicationId)
            .OrderByDescending(x => x.RevisionNumber).ThenBy(x => x.Stage).ToListAsync(cancellationToken);
        var revision = snapshots.FirstOrDefault()?.RevisionNumber;
        if (!revision.HasValue) return null;
        var before = snapshots.FirstOrDefault(x => x.RevisionNumber == revision && x.Stage == "Before");
        var after = snapshots.FirstOrDefault(x => x.RevisionNumber == revision && x.Stage == "After");
        if (before is null || after is null) return null;
        var left = JsonSerializer.Deserialize<Dictionary<string, string>>(before.DataJson) ?? [];
        var right = JsonSerializer.Deserialize<Dictionary<string, string>>(after.DataJson) ?? [];
        var fields = left.Keys.Union(right.Keys).Select(key => new RevisionFieldDifference(
            key, left.GetValueOrDefault(key, "Eksik"), right.GetValueOrDefault(key, "Eksik"),
            left.GetValueOrDefault(key) != right.GetValueOrDefault(key))).ToList();
        return new(revision.Value, fields);
    }

    public async Task<SimulationResponse> SimulateAsync(
        SimulationRequest request, CancellationToken cancellationToken)
    {
        if (request.RequestedLimit <= 0)
            throw new ArgumentException("Talep edilen limit sıfırdan büyük olmalıdır.");
        var customer = await db.Customers.AsNoTracking().Include(x => x.OtherBankCards)
            .FirstOrDefaultAsync(x => x.Id == request.CustomerId, cancellationToken)
            ?? throw new ArgumentException("Müşteri bulunamadı.");
        var cardType = await db.CardTypes.AsNoTracking().FirstOrDefaultAsync(
            x => x.Id == request.CardTypeId, cancellationToken)
            ?? throw new ArgumentException("Kart tipi bulunamadı.");
        var missing = CustomerService.GetMissingProfileFields(customer).ToList();
        var ownBankLimits = await db.CreditCards.AsNoTracking()
            .Where(x => x.CardApplication.CustomerId == customer.Id
                        && x.CardApplication.Status == ApplicationStatus.Approved)
            .Select(x => x.CardLimit).ToListAsync(cancellationToken);
        var ownBankLimit = ownBankLimits.Sum();
        var available = limitCalculator.Calculate(
            customer.MonthlyNetIncome, customer.OtherBankTotalCardLimit, ownBankLimit).AvailableLimit;
        var network = cardType.Name.Contains("Mastercard", StringComparison.OrdinalIgnoreCase) ? "Mastercard"
            : cardType.Name.Contains("TROY", StringComparison.OrdinalIgnoreCase) ? "TROY" : "Visa";
        // Kart ağı müşterinin seçimi olarak korunur; ürün seviyesi ise gelir, mevcut limit,
        // kredi skoru ve talep tutarının birlikte oluşturduğu erişilebilir banda göre önerilir.
        var qualificationAmount = Math.Min(available, request.RequestedLimit);
        var suggestedTier = customer.CreditScore < 1100 || qualificationAmount < 50_000 ? "Classic"
            : qualificationAmount < 150_000 ? "Gold"
            : qualificationAmount < 300_000 ? "Platinum" : "Platinum Plus";
        var suggestedCard = $"{suggestedTier} {network}";
        var assessment = CalculatePreAssessment(
            customer, cardType.Name, request.RequestedLimit, available);
        var risks = assessment.RiskFactors.ToList();
        if (request.RequestedLimit > available)
            risks.Add("Talep edilen limit müşterinin hesaplanan kullanılabilir limitini aşıyor.");
        if (customer.OtherBankTotalCardLimit > customer.MonthlyNetIncome * 2)
            risks.Add("Diğer banka kart limitlerinin gelire oranı yüksek.");
        foreach (var field in missing)
            risks.Add($"{field} bilgisi eksik veya doğrulanmamış.");
        var recommendations = new List<string>();
        if (request.RequestedLimit > available)
            recommendations.Add($"Talep edilen limiti {available:N2} TL veya altına düşürün.");
        if (missing.Count > 0)
            recommendations.Add("Eksik müşteri bilgilerini ve belgelerini başvuru öncesinde tamamlayın.");
        if (!string.Equals(cardType.Name, suggestedCard, StringComparison.OrdinalIgnoreCase))
            recommendations.Add($"Müşteri profiline daha uygun olan {suggestedCard} kartı değerlendirin.");
        if (customer.MonthlyNetIncome <= 0)
            recommendations.Add("Gelir bilgisini güncelleyin ve doğrulayın.");
        if (customer.CreditScore < 1100)
            recommendations.Add("Kredi riskini manuel olarak inceleyin.");
        if (recommendations.Count == 0)
            recommendations.Add("Mevcut bilgilerle başvuru sürecine devam edilebilir.");
        return new(missing.Count == 0 && request.RequestedLimit > 0 && request.RequestedLimit <= available,
            missing, suggestedCard, Math.Max(5_000, available * .50m), available, assessment,
            risks.Distinct().ToList(), recommendations.Distinct().ToList(),
            "Simülasyon gerçek kayıt oluşturmaz ve müşteri başvuru geçmişine eklenmez.");
    }

    public async Task<UserProfileResponse> GetProfileAsync(int userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Kullanıcı bulunamadı.");
        return await MapProfileAsync(user, cancellationToken);
    }

    public async Task<UserProfileResponse> UpdateProfileAsync(
        int userId, UpdateProfilePreferencesRequest request, CancellationToken cancellationToken)
    {
        var user = await db.Users.Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Kullanıcı bulunamadı.");
        user.PhoneNumber = request.PhoneNumber.Trim();
        user.ProfilePhotoUrl = request.ProfilePhotoUrl?.Trim();
        user.NotifyApplicationEvents = request.NotifyApplicationEvents;
        user.NotifySlaWarnings = request.NotifySlaWarnings;
        user.NotifySecurityEvents = request.NotifySecurityEvents;
        user.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return await MapProfileAsync(user, cancellationToken);
    }

    public async Task<GeneratedPdfResponse> GenerateApplicationPdfAsync(
        int applicationId, bool sendEmail, CancellationToken cancellationToken)
    {
        // Eski ve demo kayıtlar da PDF'e sıfır puanla düşmesin. Rapor her
        // üretildiğinde güncel müşteri ve başvuru verileriyle puanı yeniler.
        await GetPreAssessmentAsync(applicationId, cancellationToken);
        var application = await db.CardApplications.AsNoTracking()
            .Include(x => x.Customer).Include(x => x.CardType)
            .FirstOrDefaultAsync(x => x.Id == applicationId, cancellationToken)
            ?? throw new ArgumentException("Başvuru bulunamadı.");
        var fileName = $"{application.ApplicationNumber}-basvuru-ozeti.pdf";
        var content = BuildPdf("Kredi Kartı Başvuru Özeti", application.ApplicationNumber,
        [
            ("Müşteri", $"{application.Customer.FirstName} {application.Customer.LastName}"),
            ("Müşteri No", application.Customer.CustomerNumber),
            ("Kart Tipi", application.CardType.Name),
            ("Talep Edilen Limit", $"{application.RequestedLimit:N2} TL"),
            ("Başvuru Durumu", TurkishStatus(application.Status.ToString())),
            ("Başvuru Tarihi", application.CreatedAtUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm")),
            ("Teslimat", application.DeliveryAddress),
            ("Ekstre", $"{TurkishStatement(application.StatementPreference)} / Ayın {application.StatementDay}. günü"),
            ("Ön Değerlendirme", $"{application.PreAssessmentScore}/100 - {application.PreAssessmentRiskLevel}")
        ], DecisionSupportDisclaimer);
        await StoreAndMaybeEmailAsync("Application", application.Id, "ApplicationSummary", fileName,
            content, application.Customer.EmailAddress, sendEmail, cancellationToken);
        return new(content, fileName);
    }

    public async Task<GeneratedPdfResponse> GenerateCardPdfAsync(
        int cardId, bool sendEmail, CancellationToken cancellationToken)
    {
        var card = await db.CreditCards.AsNoTracking()
            .Include(x => x.CardApplication).ThenInclude(x => x.Customer)
            .Include(x => x.CardApplication).ThenInclude(x => x.CardType)
            .Include(x => x.Fulfillment)
            .FirstOrDefaultAsync(x => x.Id == cardId, cancellationToken)
            ?? throw new ArgumentException("Kart bulunamadı.");
        var fileName = $"{card.CardApplication.ApplicationNumber}-kart-bilgileri.pdf";
        var content = BuildPdf("Kredi Kartı Bilgi Formu", card.CardApplication.ApplicationNumber,
        [
            ("Kart Sahibi", $"{card.CardApplication.Customer.FirstName} {card.CardApplication.Customer.LastName}"),
            ("Kart Numarası", card.MaskedCardNumber),
            ("Kart Tipi", card.CardApplication.CardType.Name),
            ("Kart Limiti", $"{card.CardLimit:N2} TL"),
            ("Son Kullanma", card.ExpiryDate.ToString("MM/yy")),
            ("Kart Durumu", TurkishCardStatus(card.Status.ToString())),
            ("Temassız", card.CardApplication.ContactlessEnabled ? "Açık" : "Kapalı"),
            ("İnternet Alışverişi", card.CardApplication.InternetShoppingEnabled ? "Açık" : "Kapalı"),
            ("Hesap Kesim / Son Ödeme",
                $"Ayın {card.CardApplication.StatementDay}. günü / +10 gün"),
            ("Tahmini Teslim", (card.Fulfillment?.EstimatedDeliveryAtUtc ?? DateTime.UtcNow.AddDays(3))
                .ToLocalTime().ToString("dd.MM.yyyy HH:mm"))
        ], "Güvenlik nedeniyle kartın tam numarası ve güvenlik kodu bu belgede yer almaz.",
            new CardPdfVisual(
                $"{card.CardApplication.Customer.FirstName} {card.CardApplication.Customer.LastName}",
                card.MaskedCardNumber,
                card.CardApplication.CardType.Name,
                card.ExpiryDate.ToString("MM/yy"),
                card.CardApplication.ContactlessEnabled));
        await StoreAndMaybeEmailAsync("Card", card.Id, "CardInformation", fileName,
            content, card.CardApplication.Customer.EmailAddress, sendEmail, cancellationToken);
        return new(content, fileName);
    }

    public async Task NotifyApplicationEventAsync(
        int applicationId, string eventName, CancellationToken cancellationToken)
    {
        var application = await db.CardApplications.AsNoTracking()
            .Include(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Id == applicationId, cancellationToken)
            ?? throw new ArgumentException("Başvuru bulunamadı.");
        var linkForManager = $"/manager/applications/{application.Id}";
        var linkForOfficer = $"/officer/applications/{application.Id}";
        if (eventName is "Created" or "Resubmitted")
            await NotifyRoleAsync("Manager",
                eventName == "Created" ? "Yeni başvuru" : "Başvuru revizyondan döndü",
                $"{application.ApplicationNumber} numaralı başvuru {(eventName == "Created" ? "değerlendirme bekliyor" : "yeniden gönderildi")}.",
                linkForManager, cancellationToken);
        else
            await NotifyUserAsync(application.CreatedByUserId, "Başvuru sonucu",
                $"{application.ApplicationNumber} numaralı başvuru: {TurkishStatus(application.Status.ToString())}.",
                linkForOfficer, cancellationToken);
        if (eventName is "Approved" or "Rejected")
            await SendCustomerApplicationStatusAsync(application, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public static CardFulfillment CreateFulfillment(CreditCard card)
    {
        var now = DateTime.UtcNow;
        var fulfillment = new CardFulfillment
        {
            CreditCard = card, CreditCardId = card.Id, Status = "Production",
            ProductionStartedAtUtc = now, EstimatedPrintAtUtc = now.AddHours(4),
            EstimatedDeliveryAtUtc = now.AddDays(3), NextTransitionAtUtc = now.AddHours(2)
        };
        card.Fulfillment = fulfillment;
        return fulfillment;
    }

    public static void AdvanceFulfillment(CardFulfillment item, DateTime now)
    {
        var changed = true;
        var anyChanged = false;
        while (changed && item.Status != "Delivered")
        {
            changed = false;
            if (item.Status == "Production" && now >= item.NextTransitionAtUtc)
            {
                item.Status = "Printing"; item.PrintingStartedAtUtc = item.NextTransitionAtUtc;
                item.NextTransitionAtUtc = item.EstimatedPrintAtUtc;
                changed = true; anyChanged = true;
            }
            else if (item.Status == "Printing" && now >= item.NextTransitionAtUtc)
            {
                item.Status = "Shipped"; item.PrintedAtUtc = item.NextTransitionAtUtc;
                item.ShippedAtUtc = item.NextTransitionAtUtc; item.TrackingNumber ??= $"KBT{item.CreditCardId:D8}";
                item.NextTransitionAtUtc = item.EstimatedDeliveryAtUtc;
                changed = true; anyChanged = true;
            }
            else if (item.Status == "Shipped" && now >= item.NextTransitionAtUtc)
            {
                item.Status = "Delivered"; item.DeliveredAtUtc = item.NextTransitionAtUtc;
                changed = true; anyChanged = true;
            }
        }
        if (anyChanged) item.UpdatedAtUtc = now;
    }

    private async Task<UserProfileResponse> MapProfileAsync(User user, CancellationToken cancellationToken)
    {
        var role = user.UserRoles.Select(x => x.Role.Name).FirstOrDefault() ?? "Officer";
        var logins = await db.LoginHistories.AsNoTracking().Where(x => x.UserId == user.Id)
            .OrderByDescending(x => x.CreatedAtUtc).Take(10).ToListAsync(cancellationToken);
        var audits = await db.AuditLogs.AsNoTracking().Where(x => x.UserId == user.Id)
            .OrderByDescending(x => x.CreatedAtUtc).Take(8).ToListAsync(cancellationToken);
        var granted = role == "Manager"
            ? new[] { "Tüm başvuruları görüntüleme", "Başvuru onaylama", "Başvuru reddetme", "Revizyon isteme", "Rapor ve istatistik görüntüleme" }
            : new[] { "Müşteri arama", "Yeni müşteri oluşturma", "Müşteri bilgilerini güncelleme", "Kart başvurusu oluşturma", "Kendi başvurularını görüntüleme", "Revizyon başvurusunu düzenleme" };
        var denied = role == "Manager"
            ? new[] { "Kullanıcı şifresi görüntüleme", "Müşteri kartının tam numarasını görüntüleme" }
            : new[] { "Başvuru onaylama", "Başvuru reddetme", "Kullanıcı hesabı yönetme" };
        int? activeOfficers = null, applicationsToday = null, revisionWaiting = null, overdue = null;
        if (role == "Manager")
        {
            activeOfficers = await db.Users.CountAsync(x => x.IsActive && x.UserRoles.Any(r => r.Role.Name == "Officer"), cancellationToken);
            applicationsToday = await db.CardApplications.CountAsync(x => x.CreatedAtUtc >= DateTime.UtcNow.Date, cancellationToken);
            revisionWaiting = await db.CardApplications.CountAsync(x => x.Status == ApplicationStatus.Revision, cancellationToken);
            overdue = await db.CardApplications.CountAsync(x =>
                (x.Status == ApplicationStatus.Pending || x.Status == ApplicationStatus.Revision)
                && x.CreatedAtUtc < DateTime.UtcNow.AddHours(-8), cancellationToken);
        }
        return new(user.Id, user.FullName, user.RegistrationNumber, role, user.CorporateEmail,
            user.PhoneNumber, user.Title, user.Department, user.Branch, user.IsActive, user.CreatedAtUtc,
            logins.FirstOrDefault(x => x.IsSuccessful)?.CreatedAtUtc,
            logins.FirstOrDefault(x => !x.IsSuccessful)?.CreatedAtUtc,
            user.PasswordChangedAtUtc, user.TwoFactorEnabled,
            user.LockoutEndUtc > DateTime.UtcNow,
            logins.Select(x => new LoginHistoryItem(x.CreatedAtUtc, x.IsSuccessful, "Chrome / macOS", x.IpAddress)).ToList(),
            audits.Select(x => new ProfileActivityItem(x.CreatedAtUtc,
                $"{x.Action} — {x.EntityName}{(string.IsNullOrWhiteSpace(x.EntityId) ? "" : $" #{x.EntityId}")}")).ToList(),
            granted, denied, activeOfficers, applicationsToday, revisionWaiting, overdue,
            user.NotifyApplicationEvents, user.NotifySlaWarnings, user.NotifySecurityEvents);
    }

    private static PreAssessmentResponse CalculatePreAssessment(
        Customer customer, string cardType, decimal requestedLimit, decimal available)
    {
        var score = 50;
        var positive = new List<string>();
        var risk = new List<string>();
        if (customer.MonthlyNetIncome >= 40_000) { score += 12; positive.Add("Gelir seviyesi düzenli ve yeterli."); }
        else if (customer.MonthlyNetIncome >= 20_000) { score += 6; positive.Add("Gelir bilgisi mevcut."); }
        else { score -= 8; risk.Add("Gelir seviyesi düşük."); }
        var activelyEmployed = customer.EmploymentStatus is "Aktif" or "Çalışıyor" or "Emekli - Çalışıyor";
        if (activelyEmployed && !string.IsNullOrWhiteSpace(customer.Occupation))
        { score += 10; positive.Add("Aktif çalışma durumu."); }
        else { score -= 10; risk.Add("Çalışma durumu belirsiz veya aktif değil."); }
        var age = customer.BirthDate.HasValue ? DateTime.UtcNow.Year - customer.BirthDate.Value.Year : 0;
        if (age is >= 23 and <= 65) { score += 6; positive.Add("Yaş kriteri dengeli."); }
        else risk.Add("Yaş bilgisi ek inceleme gerektiriyor.");
        if (customer.IsPhoneVerified && customer.IsEmailVerified)
        { score += 8; positive.Add("Telefon ve e-posta doğrulanmış."); }
        else { score -= 8; risk.Add("İletişim doğrulaması eksik."); }
        if (requestedLimit <= available * .70m)
        {
            score += 12;
            positive.Add("Talep edilen limit gelir ve mevcut limitlerle uyumlu.");
        }
        else if (available <= 0)
        {
            score -= 60;
            risk.Add("Müşterinin kullanılabilir limit kapasitesi bulunmuyor.");
        }
        else if (requestedLimit > available * 4)
        {
            score -= 55;
            risk.Add("Talep edilen limit önerilen azami limitin dört katından fazla; kritik limit uyumsuzluğu var.");
        }
        else if (requestedLimit > available * 2)
        {
            score -= 42;
            risk.Add("Talep edilen limit önerilen azami limitin iki katından fazla; yüksek limit riski var.");
        }
        else if (requestedLimit > available)
        {
            score -= 28;
            risk.Add("Talep edilen limit önerilen azami limitin üzerinde.");
        }
        else
        {
            score -= 5;
            risk.Add("Talep edilen limit önerilen limitin üst bandında.");
        }
        if (cardType.StartsWith("Platinum", StringComparison.OrdinalIgnoreCase)
            && customer.MonthlyNetIncome < 50_000)
        { score -= 10; risk.Add($"{cardType} kart talebi gelir seviyesine göre yüksek segmentte."); }
        if (CustomerService.GetMissingProfileFields(customer).Count > 0)
        { score -= 20; risk.Add("Eksik müşteri bilgileri bulunuyor."); }
        score = Math.Clamp(score, 0, 100);
        var (level, recommendation) = score switch
        {
            >= 75 => ("LOW", "APPROVAL_RECOMMENDED"),
            >= 50 => ("MEDIUM", "MANUAL_REVIEW"),
            _ => ("HIGH", "CAUTION_RECOMMENDED")
        };
        return new(score, level, recommendation, positive, risk, DecisionSupportDisclaimer);
    }

    private static SupplementaryApplicationResponse Map(SupplementaryCardApplication x) =>
        new(x.Id, x.ApplicationNumber, x.PrimaryCreditCardId, x.PrimaryCustomerId,
            $"{x.PrimaryCustomer.FirstName} {x.PrimaryCustomer.LastName}", x.SupplementaryHolderCustomerId,
            $"{x.SupplementaryHolderCustomer.FirstName} {x.SupplementaryHolderCustomer.LastName}",
            x.Relationship, x.RequestedLimit, x.DeliveryMethod, x.DeliveryAddress,
            x.Status, x.CreatedAtUtc, x.EvaluationNote, x.EvaluatedAtUtc,
            x.MaskedCardNumber, x.IssuedAtUtc, x.CardStatus, x.FulfillmentStatus,
            x.EstimatedPrintAtUtc, x.EstimatedDeliveryAtUtc, x.DeliveredAtUtc, x.TrackingNumber);

    public static void AdvanceSupplementaryFulfillment(SupplementaryCardApplication item, DateTime now)
    {
        while (item.FulfillmentStatus is not null and not "Delivered"
               && item.NextTransitionAtUtc.HasValue && now >= item.NextTransitionAtUtc.Value)
        {
            if (item.FulfillmentStatus == "Production")
            {
                item.FulfillmentStatus = "Printing";
                item.PrintingStartedAtUtc = item.NextTransitionAtUtc;
                item.NextTransitionAtUtc = item.EstimatedPrintAtUtc;
            }
            else if (item.FulfillmentStatus == "Printing")
            {
                item.FulfillmentStatus = "Shipped";
                item.ShippedAtUtc = item.NextTransitionAtUtc;
                item.TrackingNumber ??= $"EKK{item.Id:D8}";
                item.NextTransitionAtUtc = item.EstimatedDeliveryAtUtc;
            }
            else if (item.FulfillmentStatus == "Shipped")
            {
                item.FulfillmentStatus = "Delivered";
                item.DeliveredAtUtc = item.NextTransitionAtUtc;
                item.NextTransitionAtUtc = null;
            }
        }
    }

    private static void ValidateSupplementaryRelationshipAge(Customer primary, Customer holder, string relationship)
    {
        // Yaş farkı tek başına hukuki yakınlığı kanıtlamaz. Evlat edinme ve üvey
        // ebeveynlik gibi geçerli istisnalar nedeniyle bu kontrol başvuruyu bloke etmez.
        _ = primary;
        _ = holder;
        _ = relationship;
    }

    private static ChatMessageResponse Map(ChatMessage x) =>
        new(x.Id, x.SenderUserId, x.SenderUser.FullName, x.RecipientUserId, x.RecipientUser.FullName,
            x.Message, x.CardApplicationId, x.IsRead, x.CreatedAtUtc);

    private static ConsentResponse Map(CustomerConsent x) =>
        new(x.Id, x.ConsentType, x.TextVersion, x.IsGranted, x.Channel,
            x.CapturedByUser.FullName, x.CapturedAtUtc, x.WithdrawnAtUtc);

    private static SlaItemResponse ToSlaItem(CardApplication x, DateTime now)
    {
        var statusStartedAt = ApplicationOperationsPolicy.GetCurrentStageStartedAt(x);
        var hours = Math.Max(0, (now - statusStartedAt).TotalHours);
        var status = hours > 8 ? "Overdue" : hours >= 4 ? "Attention" : "Normal";
        return new(x.Id, x.ApplicationNumber, $"{x.Customer.FirstName} {x.Customer.LastName}",
            x.Status.ToString(), Math.Round(hours, 1), status, x.CreatedAtUtc);
    }

    private static FulfillmentResponse Map(CardFulfillment x)
    {
        var rank = x.Status switch { "Production" => 0, "Printing" => 1, "Shipped" => 2, _ => 3 };
        string State(int index) => index < rank ? "Completed" : index == rank ? "Active" : "Pending";
        return new(x.CreditCardId, x.Status, x.EstimatedPrintAtUtc, x.EstimatedDeliveryAtUtc, x.TrackingNumber,
        [
            new("Production", "Üretim", State(0), x.ProductionStartedAtUtc),
            new("Printing", "Basım", State(1), x.PrintingStartedAtUtc),
            new("Shipped", "Sevk", State(2), x.ShippedAtUtc),
            new("Delivered", "Teslim", State(3), x.DeliveredAtUtc)
        ]);
    }

    private async Task NotifyRoleAsync(string role, string title, string message, string link, CancellationToken cancellationToken)
    {
        var userIds = await db.Users.Where(x => x.IsActive && x.UserRoles.Any(r => r.Role.Name == role))
            .Select(x => x.Id).ToListAsync(cancellationToken);
        foreach (var id in userIds) db.Notifications.Add(NewNotification(id, title, message, link));
    }

    private Task NotifyUserAsync(int userId, string title, string message, string link, CancellationToken cancellationToken)
    {
        db.Notifications.Add(NewNotification(userId, title, message, link));
        return Task.CompletedTask;
    }

    private static Notification NewNotification(int userId, string title, string message, string link) =>
        new() { UserId = userId, Type = "Application", Title = title, Message = message, Link = link };

    private static string RolePath(User user) =>
        user.UserRoles.Any(x => x.Role.Name == "Manager") ? "manager" : "officer";

    private async Task ValidateChatApplicationReferenceAsync(int? applicationId, CancellationToken cancellationToken)
    {
        if (applicationId.HasValue && !await db.CardApplications.AsNoTracking()
                .AnyAsync(x => x.Id == applicationId.Value, cancellationToken))
            throw new ArgumentException("İlgili başvuru bulunamadı. Geçerli bir başvuru seçin veya alanı boş bırakın.");
    }

    private async Task ValidateChatRateAsync(int userId, string message, CancellationToken cancellationToken)
    {
        var oneMinuteAgo = DateTime.UtcNow.AddMinutes(-1);
        var recent = await db.ChatMessages.AsNoTracking()
            .Where(x => x.SenderUserId == userId && x.CreatedAtUtc >= oneMinuteAgo)
            .Select(x => x.Message).ToListAsync(cancellationToken);
        if (recent.Count >= 10)
            throw new InvalidOperationException("Çok kısa sürede çok fazla mesaj gönderdiniz. Bir dakika sonra tekrar deneyin.");
        var normalized = message.Trim();
        if (recent.Count(x => string.Equals(x.Replace("[BİLGİ/CC] ", "", StringComparison.Ordinal), normalized,
                StringComparison.OrdinalIgnoreCase)) >= 3)
            throw new InvalidOperationException("Aynı mesaj art arda gönderildiği için spam koruması devreye girdi.");
    }

    private static void ValidateChatMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message) || message.Trim().Length > 1000)
            throw new ArgumentException("Mesaj 1-1000 karakter arasında olmalıdır.");
        var normalized = message.Trim().ToLower(new System.Globalization.CultureInfo("tr-TR"));
        var moderationTerms = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["hakaret veya taciz"] = ["aptal", "salak", "gerizekalı", "hakaret", "küfür"],
            ["kimlik bilgisi talebi"] = ["şifreni gönder", "parolanı gönder", "sms kodunu gönder", "doğrulama kodunu gönder"],
            ["kart güvenliği ihlali"] = ["cvv gönder", "kart numarasının tamamı", "kart şifreni yaz"],
            ["şüpheli yönlendirme"] = ["hemen para gönder", "ödül kazandın tıkla", "hesabın kapanacak tıkla"]
        };
        var violation = moderationTerms.FirstOrDefault(category =>
            category.Value.Any(term => normalized.Contains(term, StringComparison.Ordinal)));
        if (violation.Key is not null)
            throw new ArgumentException($"Mesaj {violation.Key} kuralına takıldığı için gönderilemedi.");
    }

    private static string TurkishStatus(string value) => value switch
    {
        "Pending" => "Bekliyor", "Approved" => "Onaylandı", "Rejected" => "Reddedildi",
        "Revision" => "Revizyon", _ => value
    };

    private static string TurkishCardStatus(string value) => value switch
    {
        "Inactive" => "Pasif", "Active" => "Aktif", "Blocked" => "Bloke",
        "Expired" => "Süresi Dolmuş", "Cancelled" => "İptal",
        "0" or "1" => "Pasif", "2" => "Aktif", "3" => "Bloke", "4" => "Süresi Dolmuş", "5" => "İptal", _ => value
    };

    private static string TurkishStatement(string value) =>
        value == "Paper" ? "Basılı Ekstre" : value == "Mobile" ? "Mobil Bildirim" : "E-posta";

    private sealed record CardPdfVisual(
        string Holder, string Number, string Type, string Expiry, bool Contactless);

    private const string ContactlessSvg =
        """
        <svg viewBox="0 0 30 30" xmlns="http://www.w3.org/2000/svg">
          <g fill="none" stroke="#FFFFFF" stroke-width="2.2" stroke-linecap="round">
            <path d="M8 11c2.8 2.2 2.8 5.8 0 8"/>
            <path d="M13 7c5.6 4.4 5.6 11.6 0 16"/>
            <path d="M18 3c8.4 6.6 8.4 17.4 0 24"/>
          </g>
        </svg>
        """;

    private static byte[] BuildPdf(
        string title,
        string reference,
        IReadOnlyList<(string Label, string Value)> rows,
        string note,
        CardPdfVisual? cardVisual = null) =>
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(cardVisual is null ? 38 : 32);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.BlueGrey.Darken4));
                page.Header().Column(column =>
                {
                    column.Item().Text("KART BAŞVURU YÖNETİM SİSTEMİ")
                        .FontSize(8).SemiBold().LetterSpacing(.12f).FontColor("#A17C22");
                    column.Item().PaddingTop(5).Text(title).FontSize(cardVisual is null ? 24 : 21)
                        .Bold().FontColor("#0B2748");
                    column.Item().PaddingTop(cardVisual is null ? 13 : 9)
                        .Background("#0B2A4D").Padding(cardVisual is null ? 12 : 10).Row(row =>
                    {
                        row.RelativeItem().Text("BELGE REFERANSI").FontSize(8).SemiBold().FontColor("#AFC4D8");
                        row.RelativeItem().AlignRight().Text(reference).FontSize(12).Bold().FontColor(Colors.White);
                    });
                });
                page.Content().PaddingVertical(cardVisual is null ? 20 : 13).Column(column =>
                {
                    column.Spacing(cardVisual is null ? 10 : 7);
                    if (cardVisual is not null)
                    {
                        column.Item().AlignCenter().Width(320).Height(178)
                            .Background("#0B2A4D").Padding(18).Column(card =>
                        {
                            card.Item().Row(row =>
                            {
                                row.RelativeItem().Text("KART BAŞVURU SİSTEMİ")
                                    .FontSize(9).SemiBold().FontColor("#D9E6F1");
                                row.RelativeItem().AlignRight().Text(cardVisual.Type.ToUpperInvariant())
                                    .FontSize(10).Bold().FontColor(Colors.White);
                            });
                            card.Item().PaddingTop(15).Row(row =>
                            {
                                row.ConstantItem(44).Height(30).Background("#D8B769");
                                row.RelativeItem();
                                if (cardVisual.Contactless)
                                    row.ConstantItem(30).Height(30).Svg(ContactlessSvg);
                            });
                            card.Item().PaddingTop(12).Text(cardVisual.Number)
                                .FontSize(17).Bold().LetterSpacing(.08f).FontColor(Colors.White);
                            card.Item().PaddingTop(12).Row(row =>
                            {
                                row.RelativeItem().Column(value =>
                                {
                                    value.Item().Text("KART SAHİBİ").FontSize(7).FontColor("#9EB5C9");
                                    value.Item().Text(cardVisual.Holder.ToUpperInvariant())
                                        .FontSize(9).SemiBold().FontColor(Colors.White);
                                });
                                row.ConstantItem(75).Column(value =>
                                {
                                    value.Item().Text("SON KULLANMA").FontSize(7).FontColor("#9EB5C9");
                                    value.Item().Text(cardVisual.Expiry).FontSize(9).SemiBold().FontColor(Colors.White);
                                });
                            });
                        });
                    }

                    for (var index = 0; index < rows.Count; index += 2)
                    {
                        var left = rows[index];
                        var right = index + 1 < rows.Count ? rows[index + 1] : default;
                        column.Item().Row(row =>
                        {
                            row.RelativeItem().Element(box => PdfInfoBox(box, left.Label, left.Value));
                            row.ConstantItem(10);
                            if (index + 1 < rows.Count)
                                row.RelativeItem().Element(box => PdfInfoBox(box, right.Label, right.Value));
                            else
                                row.RelativeItem();
                        });
                    }
                    column.Item().PaddingTop(cardVisual is null ? 16 : 8).BorderLeft(3).BorderColor("#D4A937")
                        .Background("#FFF9E9").Padding(cardVisual is null ? 12 : 9).Text(note).FontSize(9);
                });
                page.Footer().DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Darken1))
                    .AlignCenter().Text(text =>
                {
                    text.Span("Bu belge elektronik ortamda güvenli özet olarak oluşturulmuştur - ");
                    text.CurrentPageNumber();
                    text.Span("/");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();

    private static void PdfInfoBox(IContainer container, string label, string value)
    {
        container.MinHeight(49).Border(1).BorderColor("#DFE6EC")
            .Background("#F7F9FB").Padding(9).Column(column =>
            {
                column.Item().Text(label.ToUpperInvariant())
                    .FontSize(7).SemiBold().LetterSpacing(.08f).FontColor("#7C8C9C");
                column.Item().PaddingTop(6).Text(value).FontSize(10).SemiBold().FontColor("#17334E");
            });
    }

    private async Task StoreAndMaybeEmailAsync(
        string entityType, int entityId, string documentType, string fileName, byte[] content,
        string emailAddress, bool sendEmail, CancellationToken cancellationToken)
    {
        var root = Path.Combine(Directory.GetCurrentDirectory(), "App_Data");
        var documentDirectory = Path.Combine(root, "documents");
        Directory.CreateDirectory(documentDirectory);
        var path = Path.Combine(documentDirectory, fileName);
        await File.WriteAllBytesAsync(path, content, cancellationToken);
        var record = new GeneratedDocument
        {
            EntityType = entityType, EntityId = entityId, DocumentType = documentType,
            FileName = fileName, StoragePath = path,
            Sha256 = Convert.ToHexString(SHA256.HashData(content))
        };
        db.GeneratedDocuments.Add(record);
        if (sendEmail)
        {
            try
            {
                await SendEmailAsync(emailAddress, $"Kart Başvuru Sistemi - {fileName}",
                    "Talep ettiğiniz güvenli özet belgesi ekte yer almaktadır.", fileName, content, cancellationToken);
                record.EmailStatus = "Sent";
                record.EmailedAtUtc = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                record.EmailStatus = "Failed";
                record.EmailFailureReason = ex.Message[..Math.Min(500, ex.Message.Length)];
            }
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SendCustomerApplicationStatusAsync(
        CardApplication application, CancellationToken cancellationToken)
    {
        var status = TurkishStatus(application.Status.ToString());
        var message =
            $"{application.ApplicationNumber} numaralı kredi kartı başvurunuz {status.ToLowerInvariant()}.";
        var emailConsent = await HasCurrentConsentAsync(application.CustomerId, "EMAIL", cancellationToken);
        var smsConsent = await HasCurrentConsentAsync(application.CustomerId, "SMS", cancellationToken);
        if (emailConsent && application.Customer.IsEmailVerified && !string.IsNullOrWhiteSpace(application.Customer.EmailAddress))
            await SendEmailMessageAsync(
                application.Customer.EmailAddress,
                $"Kart başvurunuz {status.ToLowerInvariant()}",
                $"{message}\n\nDetaylı bilgi için bankanızla iletişime geçebilirsiniz.",
                cancellationToken);

        if (smsConsent && application.Customer.IsPhoneVerified && !string.IsNullOrWhiteSpace(application.Customer.PhoneNumber))
        {
            var outbox = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "sms-outbox");
            Directory.CreateDirectory(outbox);
            var id = $"{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
            await File.WriteAllTextAsync(
                Path.Combine(outbox, $"{id}.txt"),
                $"TO: {application.Customer.PhoneCountryCode}{application.Customer.PhoneNumber}\n\n{message}",
                cancellationToken);
        }
    }

    private async Task<bool> HasCurrentConsentAsync(
        int customerId, string consentType, CancellationToken cancellationToken)
    {
        var current = await db.CustomerConsents.AsNoTracking()
            .Where(x => x.CustomerId == customerId && x.ConsentType == consentType)
            .OrderByDescending(x => x.CapturedAtUtc)
            .Select(x => (bool?)x.IsGranted)
            .FirstOrDefaultAsync(cancellationToken);
        return current == true;
    }

    private async Task SendEmailMessageAsync(
        string to, string subject, string body, CancellationToken cancellationToken)
    {
        var mode = configuration["Email:Mode"] ?? "File";
        if (!mode.Equals("Smtp", StringComparison.OrdinalIgnoreCase))
        {
            var outbox = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "outbox");
            Directory.CreateDirectory(outbox);
            var id = $"{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
            await File.WriteAllTextAsync(
                Path.Combine(outbox, $"{id}.txt"),
                $"TO: {to}\nSUBJECT: {subject}\n\n{body}",
                cancellationToken);
            return;
        }
        var host = configuration["Email:Smtp:Host"]
            ?? throw new InvalidOperationException("SMTP sunucusu yapılandırılmamış.");
        var port = int.TryParse(configuration["Email:Smtp:Port"], out var parsedPort) ? parsedPort : 587;
        using var message = new MailMessage(
            configuration["Email:From"] ?? "no-reply@kartbasvuru.local", to, subject, body);
        using var client = new SmtpClient(host, port)
        {
            EnableSsl = !string.Equals(
                configuration["Email:Smtp:EnableSsl"], "false", StringComparison.OrdinalIgnoreCase),
            Credentials = new NetworkCredential(
                configuration["Email:Smtp:Username"], configuration["Email:Smtp:Password"])
        };
        await client.SendMailAsync(message, cancellationToken);
    }

    private async Task SendEmailAsync(
        string to, string subject, string body, string attachmentName, byte[] attachment,
        CancellationToken cancellationToken)
    {
        var mode = configuration["Email:Mode"] ?? "File";
        if (!mode.Equals("Smtp", StringComparison.OrdinalIgnoreCase))
        {
            var outbox = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "outbox");
            Directory.CreateDirectory(outbox);
            var id = $"{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
            await File.WriteAllTextAsync(Path.Combine(outbox, $"{id}.txt"),
                $"TO: {to}\nSUBJECT: {subject}\n\n{body}\nATTACHMENT: {attachmentName}", cancellationToken);
            await File.WriteAllBytesAsync(Path.Combine(outbox, $"{id}-{attachmentName}"), attachment, cancellationToken);
            return;
        }
        var host = configuration["Email:Smtp:Host"] ?? throw new InvalidOperationException("SMTP sunucusu yapılandırılmamış.");
        var port = int.TryParse(configuration["Email:Smtp:Port"], out var parsedPort) ? parsedPort : 587;
        using var message = new MailMessage(configuration["Email:From"] ?? "no-reply@kartbasvuru.local", to, subject, body);
        message.Attachments.Add(new Attachment(new MemoryStream(attachment), attachmentName, "application/pdf"));
        using var client = new SmtpClient(host, port)
        {
            EnableSsl = !string.Equals(configuration["Email:Smtp:EnableSsl"], "false", StringComparison.OrdinalIgnoreCase),
            Credentials = new NetworkCredential(configuration["Email:Smtp:Username"], configuration["Email:Smtp:Password"])
        };
        await client.SendMailAsync(message, cancellationToken);
    }
}
