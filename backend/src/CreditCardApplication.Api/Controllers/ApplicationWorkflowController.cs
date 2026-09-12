using System.Security.Claims;
using CreditCardApplication.Application.Applications;
using CreditCardApplication.Application.Auditing;
using CreditCardApplication.Domain.Entities;
using CreditCardApplication.Domain.Enums;
using CreditCardApplication.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CreditCardApplication.Api.Controllers;

public sealed record AssignApplicationRequest(int OfficerUserId, string? Reason = null);
public sealed record WorkflowOfficerResponse(
    int UserId, string FullName, string RegistrationNumber, string Branch,
    int OpenWorkCount, int RevisionCount, int OverdueCount);
public sealed record WorkflowApplicationResponse(
    int Id, string ApplicationNumber, int CustomerId, string Customer,
    string CardType, decimal RequestedLimit, string Status, string RiskLevel,
    int Score, int? AssignedOfficerUserId, string? AssignedOfficer,
    DateTime? AssignedAtUtc, bool RequiresSecondApproval, string WorkflowStage,
    string Priority, double WaitingHours, string SlaStatus, DateTime CreatedAtUtc,
    int? FirstApprovedByUserId, string? FirstApprover, DateTime? FirstApprovedAtUtc,
    DateTime? EvaluatedAtUtc);
public sealed record WorkflowOverviewResponse(
    IReadOnlyList<WorkflowOfficerResponse> Officers,
    IReadOnlyList<WorkflowApplicationResponse> Applications,
    IReadOnlyList<WorkflowApplicationResponse> CompletedApplications,
    int UnassignedCount, int SecondApprovalCount, int OverdueCount);

[ApiController]
[Route("api/application-workflow")]
[Authorize(Roles = "Officer,Manager")]
public sealed class ApplicationWorkflowController(
    ApplicationDbContext db,
    AuditService auditService) : ControllerBase
{
    [Authorize(Roles = "Officer")]
    [HttpGet("my-work")]
    public async Task<IActionResult> GetMyWork(CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        var applications = await BaseApplicationQuery()
            .Where(x => (x.AssignedOfficerUserId == userId ||
                         x.AssignedOfficerUserId == null)
                        && (x.Status == ApplicationStatus.Pending || x.Status == ApplicationStatus.Revision))
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        return Ok(applications.Select(MapApplication).OrderByDescending(x => PriorityOrder(x.Priority)));
    }

    [Authorize(Roles = "Officer")]
    [HttpPost("{applicationId:int}/claim")]
    public async Task<IActionResult> Claim(int applicationId, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        var application = await db.CardApplications
            .Include(x => x.Customer).Include(x => x.CardType)
            .Include(x => x.AssignedOfficerUser).Include(x => x.FirstApprovedByUser)
            .Include(x => x.Histories)
            .FirstOrDefaultAsync(x => x.Id == applicationId, cancellationToken)
            ?? throw new ArgumentException("Başvuru bulunamadı.");
        if (application.Status is not (ApplicationStatus.Pending or ApplicationStatus.Revision))
            throw new InvalidOperationException("Yalnızca açık bir iş üstlenilebilir.");
        if (application.AssignedOfficerUserId.HasValue && application.AssignedOfficerUserId != userId)
            throw new InvalidOperationException("Bu iş başka bir memurun sorumluluğundadır.");

        if (!application.AssignedOfficerUserId.HasValue)
        {
            application.AssignedOfficerUserId = userId;
            application.AssignedByUserId = userId;
            application.AssignedAtUtc = DateTime.UtcNow;
            application.AutoAssignmentCompleted = false;
            application.LastAssignmentReason = "Memur açık iş havuzundan kendi üzerine aldı.";
            application.Histories.Add(new ApplicationHistory
            {
                PreviousStatus = application.Status,
                NewStatus = application.Status,
                ChangedByUserId = userId,
                Description = "Başvuru sorumlu memur tarafından açık iş havuzundan üstlenildi."
            });
            await db.SaveChangesAsync(cancellationToken);
            await auditService.WriteAsync(userId, "ApplicationClaimed", "CardApplication",
                application.Id.ToString(), application.ApplicationNumber,
                HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        }
        return Ok(MapApplication(application));
    }

    [Authorize(Roles = "Manager")]
    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview(CancellationToken cancellationToken)
    {
        var applications = await BaseApplicationQuery()
            .Where(x => x.Status == ApplicationStatus.Pending || x.Status == ApplicationStatus.Revision)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        var items = applications.Select(MapApplication).ToList();
        var completedItems = await BaseApplicationQuery()
            .Where(x => x.Status == ApplicationStatus.Approved || x.Status == ApplicationStatus.Rejected)
            .OrderByDescending(x => x.EvaluatedAtUtc ?? x.CreatedAtUtc)
            .Take(25)
            .ToListAsync(cancellationToken);
        var officers = await db.Users.AsNoTracking()
            .Where(x => x.IsActive && x.UserRoles.Any(role => role.Role.Name == "Officer"))
            .OrderBy(x => x.FullName)
            .Select(x => new { x.Id, x.FullName, x.RegistrationNumber, x.Branch })
            .ToListAsync(cancellationToken);
        var workload = officers.Select(officer =>
        {
            var assigned = items.Where(x => x.AssignedOfficerUserId == officer.Id).ToList();
            return new WorkflowOfficerResponse(
                officer.Id, officer.FullName, officer.RegistrationNumber, officer.Branch,
                assigned.Count, assigned.Count(x => x.Status == "Revision"),
                assigned.Count(x => x.SlaStatus == "Gecikmiş"));
        }).ToList();
        return Ok(new WorkflowOverviewResponse(
            workload,
            items.OrderByDescending(x => PriorityOrder(x.Priority)).ThenByDescending(x => x.WaitingHours).ToList(),
            completedItems.Select(MapApplication).ToList(),
            items.Count(x => !x.AssignedOfficerUserId.HasValue),
            items.Count(x => x.WorkflowStage == "SecondManagerApproval"),
            items.Count(x => x.SlaStatus == "Gecikmiş")));
    }

    [Authorize(Roles = "Manager")]
    [HttpPost("{applicationId:int}/assign")]
    public async Task<IActionResult> Assign(
        int applicationId,
        AssignApplicationRequest request,
        CancellationToken cancellationToken)
    {
        var officer = await db.Users.FirstOrDefaultAsync(x => x.Id == request.OfficerUserId && x.IsActive
            && x.UserRoles.Any(role => role.Role.Name == "Officer"), cancellationToken)
            ?? throw new ArgumentException("Aktif memur bulunamadı.");
        var application = await db.CardApplications
            .Include(x => x.Customer).Include(x => x.CardType)
            .Include(x => x.AssignedOfficerUser).Include(x => x.FirstApprovedByUser)
            .Include(x => x.Histories)
            .FirstOrDefaultAsync(x => x.Id == applicationId, cancellationToken)
            ?? throw new ArgumentException("Başvuru bulunamadı.");
        if (application.Status is not (ApplicationStatus.Pending or ApplicationStatus.Revision))
            throw new InvalidOperationException("Yalnızca açık başvurular yeniden atanabilir.");

        var isReassignment = application.AssignedOfficerUserId.HasValue
            && application.AssignedOfficerUserId != officer.Id;
        var reason = isReassignment
            ? ApplicationOperationsPolicy.ValidateReassignmentReason(request.Reason)
            : string.IsNullOrWhiteSpace(request.Reason) ? "Müdür tarafından ilk iş ataması yapıldı." : request.Reason.Trim();
        application.AssignedOfficerUserId = officer.Id;
        application.AssignedOfficerUser = officer;
        application.AssignedByUserId = CurrentUserId;
        application.AssignedAtUtc = DateTime.UtcNow;
        application.AutoAssignmentCompleted = true;
        application.LastAssignmentReason = reason;
        application.Histories.Add(new ApplicationHistory
        {
            PreviousStatus = application.Status,
            NewStatus = application.Status,
            ChangedByUserId = CurrentUserId,
            Description = $"Başvuru {officer.FullName} ({officer.RegistrationNumber}) kullanıcısına atandı. Gerekçe: {reason}"
        });
        db.Notifications.Add(new Notification
        {
            UserId = officer.Id,
            Type = "Assignment",
            Title = "Yeni iş atandı",
            Message = $"{application.ApplicationNumber} numaralı başvuru iş kuyruğunuza atandı.",
            Link = $"/officer/applications/{application.Id}"
        });
        await db.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync(CurrentUserId, isReassignment ? "ApplicationReassigned" : "ApplicationAssigned", "CardApplication",
            application.Id.ToString(), $"{application.ApplicationNumber} -> {officer.RegistrationNumber}; {reason}",
            HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return Ok(MapApplication(application));
    }

    [Authorize(Roles = "Manager")]
    [HttpPost("{applicationId:int}/auto-assign")]
    public async Task<IActionResult> AutoAssign(int applicationId, CancellationToken cancellationToken)
    {
        var application = await db.CardApplications
            .Include(x => x.Customer).Include(x => x.CardType)
            .Include(x => x.AssignedOfficerUser).Include(x => x.FirstApprovedByUser)
            .Include(x => x.Histories)
            .FirstOrDefaultAsync(x => x.Id == applicationId, cancellationToken)
            ?? throw new ArgumentException("Başvuru bulunamadı.");
        if (application.Status is not (ApplicationStatus.Pending or ApplicationStatus.Revision))
            throw new InvalidOperationException("Yalnızca açık başvurular otomatik atanabilir.");

        var officers = await db.Users
            .Where(x => x.IsActive && x.UserRoles.Any(role => role.Role.Name == "Officer"))
            .ToListAsync(cancellationToken);
        if (officers.Count == 0) throw new InvalidOperationException("Atama yapılabilecek aktif memur bulunamadı.");
        var openCounts = await db.CardApplications
            .Where(x => x.AssignedOfficerUserId.HasValue
                && (x.Status == ApplicationStatus.Pending || x.Status == ApplicationStatus.Revision))
            .GroupBy(x => x.AssignedOfficerUserId!.Value)
            .Select(group => new { OfficerId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.OfficerId, x => x.Count, cancellationToken);
        var city = application.Customer.City.Trim();
        var officer = officers
            .OrderByDescending(x => !string.IsNullOrWhiteSpace(city)
                && x.Branch.Contains(city, StringComparison.OrdinalIgnoreCase))
            .ThenBy(x => openCounts.GetValueOrDefault(x.Id))
            .ThenBy(x => x.FullName)
            .First();
        var reason = !string.IsNullOrWhiteSpace(city)
            && officer.Branch.Contains(city, StringComparison.OrdinalIgnoreCase)
            ? $"Şube/şehir uyumu ve dengeli açık iş yükü ({openCounts.GetValueOrDefault(officer.Id)} açık iş)."
            : $"En düşük açık iş yükü ({openCounts.GetValueOrDefault(officer.Id)} açık iş).";

        application.AssignedOfficerUserId = officer.Id;
        application.AssignedOfficerUser = officer;
        application.AssignedByUserId = CurrentUserId;
        application.AssignedAtUtc = DateTime.UtcNow;
        application.AutoAssignmentCompleted = true;
        application.LastAssignmentReason = reason;
        application.Histories.Add(new ApplicationHistory
        {
            PreviousStatus = application.Status, NewStatus = application.Status,
            ChangedByUserId = CurrentUserId,
            Description = $"Otomatik iş ataması: {officer.FullName}. {reason}"
        });
        db.Notifications.Add(new Notification
        {
            UserId = officer.Id, Type = "Assignment", Title = "Yeni iş otomatik atandı",
            Message = $"{application.ApplicationNumber} numaralı başvuru iş kuyruğunuza atandı.",
            Link = $"/officer/applications/{application.Id}"
        });
        await db.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync(CurrentUserId, "ApplicationAutoAssigned", "CardApplication",
            application.Id.ToString(), $"{application.ApplicationNumber} -> {officer.RegistrationNumber}; {reason}",
            HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return Ok(MapApplication(application));
    }

    private IQueryable<CardApplication> BaseApplicationQuery() => db.CardApplications.AsNoTracking()
        .Include(x => x.Customer).Include(x => x.CardType)
        .Include(x => x.AssignedOfficerUser).Include(x => x.FirstApprovedByUser)
        .Include(x => x.Histories);

    private static WorkflowApplicationResponse MapApplication(CardApplication application)
    {
        var statusStartedAt = ApplicationOperationsPolicy.GetCurrentStageStartedAt(application);
        var waitingHours = Math.Max(0, (DateTime.UtcNow - statusStartedAt).TotalHours);
        var sla = waitingHours >= 8 ? "Gecikmiş" : waitingHours >= 4 ? "Dikkat" : "Normal";
        var stage = application.Status == ApplicationStatus.Revision ? "OfficerRevision"
            : application.RequiresSecondApproval && application.FirstApprovedByUserId.HasValue
                ? "SecondManagerApproval" : "ManagerReview";
        var priority = stage == "SecondManagerApproval" || application.PreAssessmentRiskLevel == "HIGH"
            || sla == "Gecikmiş" ? "Kritik"
            : application.Status == ApplicationStatus.Revision || sla == "Dikkat" ? "Yüksek" : "Normal";
        return new WorkflowApplicationResponse(
            application.Id, application.ApplicationNumber, application.CustomerId,
            $"{application.Customer.FirstName} {application.Customer.LastName}",
            application.CardType.Name, application.RequestedLimit, application.Status.ToString(),
            application.PreAssessmentRiskLevel, application.PreAssessmentScore,
            application.AssignedOfficerUserId, application.AssignedOfficerUser?.FullName,
            application.AssignedAtUtc, application.RequiresSecondApproval, stage,
            priority, Math.Round(waitingHours, 1), sla, application.CreatedAtUtc,
            application.FirstApprovedByUserId, application.FirstApprovedByUser?.FullName,
            application.FirstApprovedAtUtc, application.EvaluatedAtUtc);
    }

    private static int PriorityOrder(string priority) => priority switch
    {
        "Kritik" => 3,
        "Yüksek" => 2,
        _ => 1
    };

    private int CurrentUserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new UnauthorizedAccessException("Geçerli kullanıcı bilgisi bulunamadı.");
}
