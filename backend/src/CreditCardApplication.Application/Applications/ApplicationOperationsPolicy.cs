using CreditCardApplication.Domain.Entities;
using CreditCardApplication.Domain.Enums;

namespace CreditCardApplication.Application.Applications;

public static class ApplicationOperationsPolicy
{
    public static DateTime GetCurrentStageStartedAt(CardApplication application) =>
        application.Histories
            .Where(history => history.PreviousStatus != history.NewStatus
                && history.NewStatus == application.Status)
            .OrderByDescending(history => history.CreatedAtUtc)
            .Select(history => (DateTime?)history.CreatedAtUtc)
            .FirstOrDefault() ?? application.CreatedAtUtc;

    public static int DetermineEscalationLevel(double waitingHours) => waitingHours switch
    {
        >= 24 => 5,
        >= 12 => 4,
        >= 8 => 3,
        >= 6 => 2,
        >= 4 => 1,
        _ => 0
    };

    public static ApplicationStatus DetermineCancellationStatus(
        ApplicationStatus currentStatus,
        bool cardExists,
        bool isManager,
        string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 10)
            throw new ArgumentException("İptal/geri çekme gerekçesi en az 10 karakter olmalıdır.");
        if (cardExists || currentStatus == ApplicationStatus.Approved)
            throw new InvalidOperationException("Kart oluşturulmuş veya onaylanmış bir başvuru iptal edilemez.");
        if (currentStatus is ApplicationStatus.Rejected or ApplicationStatus.Withdrawn or ApplicationStatus.Cancelled)
            throw new InvalidOperationException("Sonuçlanmış bir başvurunun durumu değiştirilemez.");
        if (currentStatus is not (ApplicationStatus.Pending or ApplicationStatus.Revision))
            throw new InvalidOperationException("Yalnızca açık başvurular geri çekilebilir veya iptal edilebilir.");
        return isManager ? ApplicationStatus.Cancelled : ApplicationStatus.Withdrawn;
    }

    public static string ValidateReassignmentReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 10)
            throw new ArgumentException("Yeniden atama gerekçesi en az 10 karakter olmalıdır.");
        return reason.Trim();
    }
}
