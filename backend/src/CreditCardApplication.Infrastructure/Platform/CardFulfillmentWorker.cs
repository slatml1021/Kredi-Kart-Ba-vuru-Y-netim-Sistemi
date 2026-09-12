using CreditCardApplication.Application.Applications;
using CreditCardApplication.Domain.Entities;
using CreditCardApplication.Domain.Enums;
using CreditCardApplication.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CreditCardApplication.Infrastructure.Platform;

public sealed class CardFulfillmentWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<CardFulfillmentWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try { await ProcessAsync(stoppingToken); }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(exception, "Kart üretim/teslim işçisi çalıştırılırken hata oluştu; bir sonraki turda yeniden denenecek.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;

        var cardsWithoutFlow = await db.CreditCards
            .Include(x => x.Fulfillment)
            .Where(x => x.Fulfillment == null)
            .ToListAsync(cancellationToken);
        foreach (var card in cardsWithoutFlow)
            PlatformV2Service.CreateFulfillment(card);

        var due = await db.CardFulfillments
            .Where(x => x.Status != "Delivered" && x.NextTransitionAtUtc <= now)
            .ToListAsync(cancellationToken);
        foreach (var item in due)
            PlatformV2Service.AdvanceFulfillment(item, now);

        var supplementaryDue = await db.SupplementaryCardApplications
            .Where(x => x.FulfillmentStatus != null && x.FulfillmentStatus != "Delivered"
                        && x.NextTransitionAtUtc <= now)
            .ToListAsync(cancellationToken);
        foreach (var item in supplementaryDue)
            PlatformV2Service.AdvanceSupplementaryFulfillment(item, now);

        var openApplications = await db.CardApplications
            .Include(x => x.Histories).Include(x => x.Customer)
            .Where(x => x.Status == ApplicationStatus.Pending || x.Status == ApplicationStatus.Revision)
            .ToListAsync(cancellationToken);
        var officers = await db.Users
            .Where(x => x.IsActive && x.UserRoles.Any(r => r.Role.Name == "Officer"))
            .ToListAsync(cancellationToken);
        var openCounts = openApplications.Where(x => x.AssignedOfficerUserId.HasValue)
            .GroupBy(x => x.AssignedOfficerUserId!.Value)
            .ToDictionary(group => group.Key, group => group.Count());
        foreach (var application in openApplications.Where(x => !x.AutoAssignmentCompleted))
        {
            if (officers.Count == 0) break;
            var city = application.Customer.City.Trim();
            var officer = officers
                .OrderByDescending(x => !string.IsNullOrWhiteSpace(city)
                    && x.Branch.Contains(city, StringComparison.OrdinalIgnoreCase))
                .ThenBy(x => openCounts.GetValueOrDefault(x.Id))
                .ThenBy(x => x.FullName)
                .First();
            var reason = !string.IsNullOrWhiteSpace(city)
                && officer.Branch.Contains(city, StringComparison.OrdinalIgnoreCase)
                ? $"Şube/şehir uyumu ve dengeli iş yükü ({openCounts.GetValueOrDefault(officer.Id)} açık iş)."
                : $"En düşük açık iş yükü ({openCounts.GetValueOrDefault(officer.Id)} açık iş).";
            application.AssignedOfficerUserId = officer.Id;
            application.AssignedAtUtc = now;
            application.AutoAssignmentCompleted = true;
            application.LastAssignmentReason = reason;
            application.Histories.Add(new ApplicationHistory
            {
                PreviousStatus = application.Status, NewStatus = application.Status,
                ChangedByUserId = application.CreatedByUserId,
                Description = $"Sistem otomatik iş ataması yaptı: {officer.FullName}. {reason}"
            });
            db.Notifications.Add(new Notification
            {
                UserId = officer.Id, Type = "Assignment", Title = "Yeni iş otomatik atandı",
                Message = $"{application.ApplicationNumber} numaralı başvuru iş kuyruğunuza atandı.",
                Link = $"/officer/applications/{application.Id}"
            });
            openCounts[officer.Id] = openCounts.GetValueOrDefault(officer.Id) + 1;
        }

        var managerIds = await db.Users
            .Where(x => x.IsActive && x.NotifySlaWarnings && x.UserRoles.Any(r => r.Role.Name == "Manager"))
            .Select(x => x.Id).ToListAsync(cancellationToken);
        foreach (var application in openApplications)
        {
            var statusStartedAt = ApplicationOperationsPolicy.GetCurrentStageStartedAt(application);
            var waitingHours = Math.Max(0, (now - statusStartedAt).TotalHours);
            var level = ApplicationOperationsPolicy.DetermineEscalationLevel(waitingHours);
            if (level <= application.EscalationLevel) continue;
            application.EscalationLevel = level;
            application.LastEscalatedAtUtc = now;
            var severity = level switch
            {
                1 => "Dikkat", 2 => "Müdür takibi", 3 => "Gecikmiş", 4 => "Kritik", _ => "Acil"
            };
            if (application.AssignedOfficerUserId.HasValue)
                db.Notifications.Add(new Notification
                {
                    UserId = application.AssignedOfficerUserId.Value, Type = "Sla", Title = $"SLA: {severity}",
                    Message = $"{application.ApplicationNumber} başvurusu {waitingHours:F1} saattir bekliyor.",
                    Link = $"/officer/applications/{application.Id}"
                });
            if (level >= 2)
            foreach (var managerId in managerIds)
                db.Notifications.Add(new Notification
                {
                    UserId = managerId, Type = "Sla", Title = $"SLA: {severity}",
                    Message = $"{application.ApplicationNumber} başvurusu {waitingHours:F1} saattir bekliyor.",
                    Link = $"/manager/applications/{application.Id}"
                });
            if (level >= 3)
                application.Histories.Add(new ApplicationHistory
                {
                    PreviousStatus = application.Status, NewStatus = application.Status,
                    ChangedByUserId = application.AssignedOfficerUserId ?? application.CreatedByUserId,
                    Description = $"SLA seviye {level} ({severity}) yükseltmesi: {waitingHours:F1} saat bekleme."
                });
        }
        await db.SaveChangesAsync(cancellationToken);
    }
}
