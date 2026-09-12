using CreditCardApplication.Domain.Entities;
using CreditCardApplication.Domain.Enums;
using System.Collections.Concurrent;

namespace CreditCardApplication.Application.Cards;

public sealed class CreditCardService(ICreditCardRepository creditCardRepository)
{
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> CardLocks = new();
    public async Task<CreditCardResponse?> GetByIdAsync(int id, int? officerUserId, CancellationToken cancellationToken)
    {
        var card = await creditCardRepository.GetByIdAsync(id, cancellationToken);
        if (card is null) return null;
        if (officerUserId.HasValue
            && card.CardApplication.CreatedByUserId != officerUserId.Value
            && card.CardApplication.AssignedOfficerUserId != officerUserId.Value)
            throw new UnauthorizedAccessException("Bu kartı görüntüleme yetkiniz yok.");

        var otherOwnBankLimit = await creditCardRepository.GetOtherApprovedCardLimitAsync(
            card.CardApplication.CustomerId, card.Id, cancellationToken);
        var customerMaximumLimit = Math.Max(0,
            card.CardApplication.Customer.MonthlyNetIncome * 3
            - card.CardApplication.Customer.OtherBankTotalCardLimit
            - otherOwnBankLimit);
        var cardTypeMaximumLimit = card.CardApplication.CardType.MaximumLimit ?? customerMaximumLimit;
        return new CreditCardResponse(card.Id, card.MaskedCardNumber, card.CardLimit, card.Status.ToString(),
            card.IssueDate, card.ExpiryDate, card.CardApplicationId, card.CardApplication.ApplicationNumber,
            card.CardApplication.Customer.CustomerNumber,
            $"{card.CardApplication.Customer.FirstName} {card.CardApplication.Customer.LastName}",
            card.CardApplication.CardType.Name,
            card.CardApplication.DeliveryMethod,
            card.CardApplication.DeliveryAddress,
            card.CardApplication.StatementPreference,
            card.CardApplication.ContactlessEnabled,
            card.CardApplication.InternetShoppingEnabled,
            cardTypeMaximumLimit,
            Math.Min(cardTypeMaximumLimit, customerMaximumLimit),
            card.RequestedNewLimit,
            card.LimitIncreaseStatus,
            card.LimitChangeType,
            card.LimitIncreaseRequestedAtUtc,
            card.LimitIncreaseEvaluationNote,
            card.LimitIncreaseEvaluatedAtUtc);
    }

    public async Task<CreditCardResponse> RequestLimitIncreaseAsync(
        int id,
        CreateLimitIncreaseRequest request,
        int officerUserId,
        CancellationToken cancellationToken)
        => await RequestLimitChangeAsync(id,
            new CreateLimitChangeRequest("Increase", request.RequestedNewLimit),
            officerUserId, cancellationToken);

    public async Task<CreditCardResponse> RequestLimitChangeAsync(
        int id,
        CreateLimitChangeRequest request,
        int officerUserId,
        CancellationToken cancellationToken)
    {
        var gate = CardLocks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
        var card = await creditCardRepository.GetTrackedByIdAsync(id, cancellationToken)
            ?? throw new ArgumentException("Kart bulunamadı.");
        if (card.CardApplication.CreatedByUserId != officerUserId
            && card.CardApplication.AssignedOfficerUserId != officerUserId)
            throw new UnauthorizedAccessException("Bu kart için işlem yapma yetkiniz yok.");
        if (!card.CardApplication.Customer.IsActive)
            throw new InvalidOperationException("Pasif müşteri için kart limit işlemi yapılamaz.");
        if (card.Status is CardStatus.Blocked or CardStatus.Expired or CardStatus.Cancelled)
            throw new InvalidOperationException("Kartın mevcut durumu limit değişikliğine uygun değildir.");
        var changeType = request.ChangeType.Trim();
        if (changeType is not ("Increase" or "Decrease"))
            throw new ArgumentException("Limit değişiklik türü Increase veya Decrease olmalıdır.");
        if (changeType == "Increase" && request.RequestedNewLimit <= card.CardLimit)
            throw new ArgumentException("Artırım için yeni limit mevcut kart limitinden büyük olmalıdır.");
        if (changeType == "Decrease" && (request.RequestedNewLimit <= 0 || request.RequestedNewLimit >= card.CardLimit))
            throw new ArgumentException("Azaltım için yeni limit sıfırdan büyük ve mevcut limitten küçük olmalıdır.");
        if (card.LimitIncreaseStatus == "Pending")
            throw new InvalidOperationException("Bu kart için bekleyen bir limit artırım talebi bulunmaktadır.");

        var customer = card.CardApplication.Customer;
        var otherOwnBankLimit = await creditCardRepository.GetOtherApprovedCardLimitAsync(
            customer.Id, card.Id, cancellationToken);
        var financialMaximum = Math.Max(
            0,
            customer.MonthlyNetIncome * 3
            - customer.OtherBankTotalCardLimit
            - otherOwnBankLimit);
        var maximumNewLimit = Math.Min(card.CardApplication.CardType.MaximumLimit ?? financialMaximum, financialMaximum);
        if (changeType == "Increase" && request.RequestedNewLimit > maximumNewLimit)
            throw new InvalidOperationException(
                $"Talep edilen yeni limit müşterinin azami limitini aşamaz. En fazla {maximumNewLimit:N2} TL talep edilebilir.");

        if (changeType == "Decrease")
        {
            var reservedSupplementaryLimit = await creditCardRepository.GetReservedSupplementaryLimitAsync(
                card.Id, cancellationToken);
            if (request.RequestedNewLimit < reservedSupplementaryLimit)
                throw new InvalidOperationException(
                    $"Yeni limit açık/onaylı ek kartlara ayrılan {reservedSupplementaryLimit:N2} TL tutarın altına düşemez.");
            card.CardLimit = request.RequestedNewLimit;
            card.RequestedNewLimit = request.RequestedNewLimit;
            card.LimitChangeType = changeType;
            card.LimitIncreaseStatus = "Approved";
            card.LimitIncreaseRequestedAtUtc = DateTime.UtcNow;
            card.LimitIncreaseEvaluationNote = "Limit azaltımı müşteri talebi doğrultusunda otomatik uygulanmıştır.";
            card.LimitIncreaseEvaluatedAtUtc = DateTime.UtcNow;
            card.LimitIncreaseEvaluatedByUserId = officerUserId;
            card.UpdatedAtUtc = DateTime.UtcNow;
            await creditCardRepository.AddNotificationAsync(
                officerUserId, "Kart limiti azaltıldı",
                $"{card.MaskedCardNumber} kartının limiti {request.RequestedNewLimit:N2} TL olarak güncellendi.",
                $"/officer/cards/{card.Id}", cancellationToken);
            await creditCardRepository.SaveChangesAsync(cancellationToken);
            return await GetByIdAsync(id, officerUserId, cancellationToken)
                ?? throw new InvalidOperationException("Kart bilgisi yeniden okunamadı.");
        }

        card.RequestedNewLimit = request.RequestedNewLimit;
        card.LimitChangeType = changeType;
        card.LimitIncreaseStatus = "Pending";
        card.LimitIncreaseRequestedAtUtc = DateTime.UtcNow;
        card.LimitIncreaseEvaluationNote = null;
        card.LimitIncreaseEvaluatedAtUtc = null;
        card.LimitIncreaseEvaluatedByUserId = null;
        card.UpdatedAtUtc = DateTime.UtcNow;
        await creditCardRepository.AddRoleNotificationsAsync(
            "Manager", "Limit artırım talebi",
            $"{card.MaskedCardNumber} kartı için {request.RequestedNewLimit:N2} TL limit talebi değerlendirme bekliyor.",
            "/manager/limit-increases", cancellationToken);
        await creditCardRepository.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, officerUserId, cancellationToken)
            ?? throw new InvalidOperationException("Kart bilgisi yeniden okunamadı.");
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<IReadOnlyList<LimitIncreaseRequestResponse>> GetLimitIncreaseRequestsAsync(
        int? officerUserId,
        CancellationToken cancellationToken)
    {
        var cards = await creditCardRepository.GetLimitIncreaseRequestsAsync(officerUserId, cancellationToken);
        return cards.Select(MapLimitIncrease).ToList();
    }

    public async Task<LimitIncreaseRequestResponse> EvaluateLimitIncreaseAsync(
        int id,
        EvaluateLimitIncreaseRequest request,
        int managerUserId,
        CancellationToken cancellationToken)
    {
        var gate = CardLocks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
        var card = await creditCardRepository.GetTrackedByIdAsync(id, cancellationToken)
            ?? throw new ArgumentException("Kart bulunamadı.");
        if (card.LimitIncreaseStatus != "Pending" || !card.RequestedNewLimit.HasValue)
            throw new InvalidOperationException("Bu kart için değerlendirilebilir bir limit artırım talebi bulunmamaktadır.");

        var decision = request.Decision.Trim();
        var approved = decision.Equals("Approved", StringComparison.OrdinalIgnoreCase);
        var rejected = decision.Equals("Rejected", StringComparison.OrdinalIgnoreCase);
        if (!approved && !rejected)
            throw new ArgumentException("Karar Approved veya Rejected olmalıdır.");
        if (rejected && string.IsNullOrWhiteSpace(request.Note))
            throw new ArgumentException("Red kararı için açıklama zorunludur.");

        if (approved)
        {
            if (card.LimitChangeType != "Decrease")
            {
                var otherOwnBankLimit = await creditCardRepository.GetOtherApprovedCardLimitAsync(
                    card.CardApplication.CustomerId, card.Id, cancellationToken);
                var financialMaximum = Math.Max(0,
                    card.CardApplication.Customer.MonthlyNetIncome * 3
                    - card.CardApplication.Customer.OtherBankTotalCardLimit
                    - otherOwnBankLimit);
                var maximumNewLimit = Math.Min(card.CardApplication.CardType.MaximumLimit ?? financialMaximum, financialMaximum);
                if (card.RequestedNewLimit.Value > maximumNewLimit)
                    throw new InvalidOperationException(
                        $"Güncel azami limit {maximumNewLimit:N2} TL olduğu için talep onaylanamaz.");
            }
            card.CardLimit = card.RequestedNewLimit.Value;
            card.LimitIncreaseStatus = "Approved";
        }
        else
        {
            card.LimitIncreaseStatus = "Rejected";
        }

        card.LimitIncreaseEvaluationNote = request.Note?.Trim();
        card.LimitIncreaseEvaluatedAtUtc = DateTime.UtcNow;
        card.LimitIncreaseEvaluatedByUserId = managerUserId;
        card.UpdatedAtUtc = DateTime.UtcNow;
        await creditCardRepository.AddNotificationAsync(
            card.CardApplication.CreatedByUserId, "Limit talebi sonuçlandı",
            $"{card.MaskedCardNumber} kartının limit talebi {(approved ? "onaylandı" : "reddedildi")}.",
            $"/officer/cards/{card.Id}", cancellationToken);
        await creditCardRepository.SaveChangesAsync(cancellationToken);
        return MapLimitIncrease(card);
        }
        finally
        {
            gate.Release();
        }
    }

    private static LimitIncreaseRequestResponse MapLimitIncrease(CreditCard card) => new(
        card.Id,
        card.MaskedCardNumber,
        card.CardApplication.Customer.CustomerNumber,
        $"{card.CardApplication.Customer.FirstName} {card.CardApplication.Customer.LastName}",
        card.CardApplication.CardType.Name,
        card.CardLimit,
        card.RequestedNewLimit ?? card.CardLimit,
        card.LimitChangeType ?? "Increase",
        card.LimitIncreaseStatus ?? "Unknown",
        card.LimitIncreaseRequestedAtUtc ?? card.CreatedAtUtc,
        card.LimitIncreaseEvaluationNote,
        card.LimitIncreaseEvaluatedAtUtc);
}
