using CreditCardApplication.Application.Services;
using CreditCardApplication.Domain.Entities;
using CreditCardApplication.Domain.Enums;

namespace CreditCardApplication.Application.Applications;

public sealed class CardApplicationService(
    ICardApplicationRepository applicationRepository,
    LimitCalculator limitCalculator)
{
    public async Task<IReadOnlyList<CardTypeResponse>> GetCardTypesAsync(CancellationToken cancellationToken)
    {
        var cardTypes = await applicationRepository.GetActiveCardTypesAsync(cancellationToken);
        return cardTypes.Select(x => new CardTypeResponse(x.Id, x.Name, x.MinimumLimit, x.MaximumLimit)).ToList();
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

    public async Task<IReadOnlyList<CardApplicationResponse>> GetPendingAsync(CancellationToken cancellationToken) =>
        (await applicationRepository.GetPendingAsync(cancellationToken)).Select(x => Map(x)).ToList();

    public async Task<CardApplicationResponse> CreateAsync(
        CreateCardApplicationRequest request,
        int createdByUserId,
        CancellationToken cancellationToken)
    {
        var customer = await applicationRepository.GetCustomerAsync(request.CustomerId, cancellationToken)
            ?? throw new ArgumentException("Müşteri bulunamadı.");
        if (!customer.IsActive)
            throw new InvalidOperationException("Pasif müşteri için kart başvurusu oluşturulamaz.");

        if (await applicationRepository.HasOpenApplicationAsync(customer.Id, cancellationToken))
            throw new InvalidOperationException("Müşterinin değerlendirmesi devam eden bir kart başvurusu bulunmaktadır.");

        var cardType = await applicationRepository.GetCardTypeAsync(request.CardTypeId, cancellationToken)
            ?? throw new ArgumentException("Seçilen kart tipi bulunamadı veya kullanıma kapalı.");

        var calculation = limitCalculator.Calculate(customer.MonthlyNetIncome, customer.OtherBankTotalCardLimit);
        ValidateRequestedLimit(request.RequestedLimit, calculation.AvailableLimit, cardType);

        var application = new CardApplication
        {
            ApplicationNumber = $"KKB-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            CustomerId = customer.Id,
            Customer = customer,
            CardTypeId = cardType.Id,
            CardType = cardType,
            CreatedByUserId = createdByUserId,
            RequestedLimit = request.RequestedLimit,
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
        var application = await applicationRepository.GetTrackedByIdAsync(applicationId, cancellationToken)
            ?? throw new ArgumentException("Başvuru bulunamadı.");
        if (application.Status != ApplicationStatus.Pending)
            throw new InvalidOperationException("Yalnızca bekleyen başvurular değerlendirilebilir.");

        if (!Enum.TryParse<ApplicationStatus>(request.Decision, true, out var newStatus)
            || newStatus is not (ApplicationStatus.Approved or ApplicationStatus.Rejected or ApplicationStatus.Revision))
            throw new ArgumentException("Karar Approved, Rejected veya Revision olmalıdır.");
        if (newStatus == ApplicationStatus.Revision && string.IsNullOrWhiteSpace(request.Note))
            throw new ArgumentException("Revizyon kararı için açıklama zorunludur.");
        if (newStatus == ApplicationStatus.Approved && (!request.ApprovedLimit.HasValue || request.ApprovedLimit <= 0))
            throw new ArgumentException("Onaylanan limit sıfırdan büyük olmalıdır.");

        var availableLimit = limitCalculator.Calculate(
            application.Customer.MonthlyNetIncome, application.Customer.OtherBankTotalCardLimit).AvailableLimit;
        if (newStatus == ApplicationStatus.Approved)
        {
            ValidateRequestedLimit(request.ApprovedLimit!.Value, availableLimit, application.CardType);
            if (application.CreditCard is not null)
                throw new InvalidOperationException("Bu başvuru için zaten bir kart oluşturulmuş.");
            application.ApprovedLimit = request.ApprovedLimit;
            application.CreditCard = CreateDemoCard(application, request.ApprovedLimit.Value);
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

    public async Task<CardApplicationResponse> ResubmitAsync(
        int applicationId,
        ResubmitCardApplicationRequest request,
        int officerUserId,
        CancellationToken cancellationToken)
    {
        var application = await applicationRepository.GetTrackedByIdAsync(applicationId, cancellationToken)
            ?? throw new ArgumentException("Başvuru bulunamadı.");
        if (application.CreatedByUserId != officerUserId)
            throw new UnauthorizedAccessException("Bu başvuruyu güncelleme yetkiniz yok.");
        if (application.Status != ApplicationStatus.Revision)
            throw new InvalidOperationException("Yalnızca revizyondaki başvurular yeniden gönderilebilir.");

        var cardType = await applicationRepository.GetCardTypeAsync(request.CardTypeId, cancellationToken)
            ?? throw new ArgumentException("Seçilen kart tipi bulunamadı veya kullanıma kapalı.");
        var availableLimit = limitCalculator.Calculate(
            application.Customer.MonthlyNetIncome, application.Customer.OtherBankTotalCardLimit).AvailableLimit;
        ValidateRequestedLimit(request.RequestedLimit, availableLimit, cardType);

        var previousStatus = application.Status;
        application.CardTypeId = cardType.Id;
        application.CardType = cardType;
        application.RequestedLimit = request.RequestedLimit;
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
        await applicationRepository.SaveChangesAsync(cancellationToken);
        return Map(application, availableLimit);
    }

    private static void ValidateRequestedLimit(decimal requestedLimit, decimal availableLimit, CardType cardType)
    {
        if (requestedLimit <= 0)
            throw new ArgumentException("Talep edilen limit sıfırdan büyük olmalıdır.");
        if (requestedLimit > availableLimit)
            throw new InvalidOperationException("Talep edilen limit müşterinin kullanılabilir limitini aşamaz.");
        if (cardType.MinimumLimit.HasValue && requestedLimit < cardType.MinimumLimit.Value)
            throw new InvalidOperationException($"{cardType.Name} kart için minimum limit {cardType.MinimumLimit.Value:N2} TL'dir.");
        if (cardType.MaximumLimit.HasValue && requestedLimit > cardType.MaximumLimit.Value)
            throw new InvalidOperationException($"{cardType.Name} kart için maksimum limit {cardType.MaximumLimit.Value:N2} TL'dir.");
    }

    private static CardApplicationResponse Map(CardApplication application, decimal? availableLimit = null)
    {
        var calculatedAvailableLimit = availableLimit
            ?? Math.Max(0, application.Customer.MonthlyNetIncome * 3 - application.Customer.OtherBankTotalCardLimit);
        return new CardApplicationResponse(
            application.Id,
            application.ApplicationNumber,
            application.CustomerId,
            application.Customer.CustomerNumber,
            $"{application.Customer.FirstName} {application.Customer.LastName}",
            application.CardTypeId,
            application.CardType.Name,
            application.RequestedLimit,
            calculatedAvailableLimit,
            application.Status.ToString(),
            application.CreatedAtUtc);
    }

    private static CreditCard CreateDemoCard(CardApplication application, decimal approvedLimit)
    {
        var suffix = Random.Shared.Next(0, 10_000).ToString("D4");
        return new CreditCard
        {
            MaskedCardNumber = $"{application.CardType.Bin} **** {suffix}",
            CardLimit = approvedLimit,
            Status = CardStatus.Inactive,
            IssueDate = DateOnly.FromDateTime(DateTime.UtcNow),
            ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(4))
        };
    }
}
