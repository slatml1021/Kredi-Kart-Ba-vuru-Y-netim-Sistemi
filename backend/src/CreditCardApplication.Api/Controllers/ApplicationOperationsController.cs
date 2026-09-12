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

public sealed record VerifyDocumentRequest(string Status, string? Note, DateTime? ExpiresAtUtc);
public sealed record CancelApplicationRequest(string Reason);
public sealed record KycCheckItem(string Key, string Label, string Status, string Detail, bool Blocking);
public sealed record KycAssessmentResponse(string OverallStatus, DateTime CheckedAtUtc,
    string Disclaimer, IReadOnlyList<KycCheckItem> Checks);
public sealed record DecisionQualityManagerItem(int UserId, string Manager, int Total,
    int Approved, int Rejected, int Revision, decimal ApprovalRate, double AverageMinutes);
public sealed record DecisionQualityResponse(int PeriodDays, int TotalDecisions,
    decimal ApprovalRate, decimal RejectionRate, decimal RevisionRate, double AverageEvaluationMinutes,
    int DualApprovalCount, int OverdueOpenCount, int ReassignmentCount,
    IReadOnlyList<DecisionQualityManagerItem> Managers);
public sealed record AuditLogResponse(int Id, string Action, string EntityName, string? EntityId,
    string? Detail, string? User, string? IpAddress, DateTime CreatedAtUtc);

[ApiController]
[Route("api/application-operations")]
[Authorize(Roles = "Officer,Manager")]
public sealed class ApplicationOperationsController(
    ApplicationDbContext db,
    AuditService auditService) : ControllerBase
{
    [HttpGet("{applicationId:int}/kyc")]
    public async Task<ActionResult<KycAssessmentResponse>> GetKyc(
        int applicationId, CancellationToken cancellationToken)
    {
        var application = await db.CardApplications.AsNoTracking()
            .Include(x => x.Customer).Include(x => x.CreditCard)
            .FirstOrDefaultAsync(x => x.Id == applicationId, cancellationToken)
            ?? throw new ArgumentException("Başvuru bulunamadı.");
        EnsureApplicationAccess(application);
        var customer = application.Customer;
        var checks = new List<KycCheckItem>();
        Add(checks, "active", "Müşteri durumu", customer.IsActive,
            customer.IsActive ? "Müşteri aktif." : "Pasif müşteri işlem yapamaz.", true);
        var profileComplete = customer.BirthDate.HasValue
            && !string.IsNullOrWhiteSpace(customer.Gender) && customer.Gender != "Belirtilmedi"
            && !string.IsNullOrWhiteSpace(customer.Occupation) && customer.Occupation != "Belirtilmedi"
            && !string.IsNullOrWhiteSpace(customer.City) && !string.IsNullOrWhiteSpace(customer.District)
            && !string.IsNullOrWhiteSpace(customer.Neighborhood) && !string.IsNullOrWhiteSpace(customer.Address);
        Add(checks, "profile", "Kimlik ve profil bütünlüğü", profileComplete,
            profileComplete ? "Zorunlu müşteri alanları tamamlandı." : "Eksik kimlik, meslek veya adres alanları var.", true);
        Add(checks, "phone", "Telefon doğrulaması", customer.IsPhoneVerified,
            customer.IsPhoneVerified ? "Telefon doğrulandı." : "Telefon doğrulaması eksik.", true);
        Add(checks, "email", "E-posta doğrulaması", customer.IsEmailVerified,
            customer.IsEmailVerified ? "E-posta doğrulandı." : "E-posta doğrulaması eksik.", true);
        var kvkkGranted = await db.CustomerConsents.AsNoTracking()
            .Where(x => x.CustomerId == customer.Id && x.ConsentType == "KVKK")
            .OrderByDescending(x => x.CapturedAtUtc).Select(x => (bool?)x.IsGranted)
            .FirstOrDefaultAsync(cancellationToken) == true;
        Add(checks, "kvkk", "KVKK açık rıza", kvkkGranted,
            kvkkGranted ? "Güncel KVKK rızası mevcut." : "Güncel KVKK rızası bulunamadı.", true);

        var now = DateTime.UtcNow;
        var documents = await db.GeneratedDocuments.AsNoTracking()
            .Where(x => x.EntityType == "CardApplicationUpload" && x.EntityId == applicationId)
            .ToListAsync(cancellationToken);
        foreach (var type in new[] { "Identity", "Income", "Residence" })
        {
            var document = documents.Where(x => x.DocumentType == type)
                .OrderByDescending(x => x.CreatedAtUtc).FirstOrDefault();
            var valid = document is not null
                && document.VerificationStatus is "PendingVerification" or "Verified"
                && (!document.ExpiresAtUtc.HasValue || document.ExpiresAtUtc > now);
            var label = type switch { "Identity" => "Kimlik belgesi", "Income" => "Gelir belgesi", _ => "İkamet belgesi" };
            Add(checks, $"document-{type.ToLowerInvariant()}", label, valid,
                document is null ? "Belge yüklenmedi."
                : document.VerificationStatus == "Rejected" ? $"Belge reddedildi: {document.VerificationNote}"
                : document.ExpiresAtUtc <= now ? "Belgenin geçerlilik süresi doldu."
                : document.VerificationStatus == "Verified" ? "Belge isteğe bağlı kalite kontrolünden geçti."
                : "Belge yüklendi; müdür doğrulaması onay için zorunlu değildir.", true);
        }

        var recent = await db.CardApplications.AsNoTracking().CountAsync(x => x.CustomerId == customer.Id
            && x.Id != applicationId && x.CreatedAtUtc >= now.AddDays(-15), cancellationToken);
        checks.Add(new KycCheckItem("velocity", "Başvuru sıklığı", recent >= 3 ? "Review" : "Passed",
            recent >= 3 ? $"Son 15 günde {recent} başka başvuru bulundu; manuel inceleme önerilir."
                : $"Son 15 günde {recent} başka başvuru bulundu.", false));
        var availableLimit = Math.Max(0, customer.MonthlyNetIncome * 3 - customer.OtherBankTotalCardLimit);
        var extremeLimit = application.RequestedLimit > Math.Max(10_000, availableLimit * 2);
        checks.Add(new KycCheckItem("limit", "Limit tutarlılığı", extremeLimit ? "Review" : "Passed",
            extremeLimit ? "Talep edilen limit kullanılabilir limitin iki katından fazla."
                : "Talep edilen limit gelir ve mevcut limitlerle tutarlı.", false));

        var overall = checks.Any(x => x.Blocking && x.Status == "Failed") ? "Blocked"
            : checks.Any(x => x.Status == "Review") ? "ReviewRequired" : "Eligible";
        await auditService.WriteAsync(CurrentUserId, "KycAssessmentViewed", "CardApplication",
            applicationId.ToString(), $"KYC sonucu: {overall}", RemoteIp, cancellationToken);
        return Ok(new KycAssessmentResponse(overall, now,
            "Bu prototip kontrolü harici AML, yaptırım ve resmi kimlik servislerinin yerine geçmez; nihai karar müdüre aittir.", checks));
    }

    [Authorize(Roles = "Manager")]
    [HttpPost("{applicationId:int}/documents/{documentId:int}/verification")]
    public async Task<IActionResult> VerifyDocument(int applicationId, int documentId,
        VerifyDocumentRequest request, CancellationToken cancellationToken)
    {
        var status = request.Status.Trim();
        if (status is not ("Verified" or "Rejected"))
            throw new ArgumentException("Belge durumu Verified veya Rejected olmalıdır.");
        if (status == "Rejected" && string.IsNullOrWhiteSpace(request.Note))
            throw new ArgumentException("Reddedilen belge için açıklama zorunludur.");
        var document = await db.GeneratedDocuments.FirstOrDefaultAsync(x => x.Id == documentId
            && x.EntityType == "CardApplicationUpload" && x.EntityId == applicationId, cancellationToken)
            ?? throw new ArgumentException("Belge bulunamadı.");
        var application = await db.CardApplications.FirstOrDefaultAsync(x => x.Id == applicationId, cancellationToken)
            ?? throw new ArgumentException("Başvuru bulunamadı.");
        document.VerificationStatus = status;
        document.VerifiedByUserId = CurrentUserId;
        document.VerifiedAtUtc = DateTime.UtcNow;
        document.VerificationNote = request.Note?.Trim();
        document.ExpiresAtUtc = request.ExpiresAtUtc;
        if (status == "Rejected")
            db.Notifications.Add(new Notification
            {
                UserId = application.AssignedOfficerUserId ?? application.CreatedByUserId,
                Type = "Document", Title = "Başvuru belgesi reddedildi",
                Message = $"{application.ApplicationNumber}: {document.DocumentType} belgesi reddedildi. {document.VerificationNote}",
                Link = $"/officer/applications/{application.Id}"
            });
        await db.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync(CurrentUserId, "ApplicationDocumentVerified", "GeneratedDocument",
            documentId.ToString(), $"{status}; {document.VerificationNote}", RemoteIp, cancellationToken);
        return NoContent();
    }

    [HttpPost("{applicationId:int}/cancel")]
    public async Task<IActionResult> Cancel(int applicationId, CancelApplicationRequest request,
        CancellationToken cancellationToken)
    {
        var application = await db.CardApplications.Include(x => x.CreditCard).Include(x => x.Histories)
            .FirstOrDefaultAsync(x => x.Id == applicationId, cancellationToken)
            ?? throw new ArgumentException("Başvuru bulunamadı.");
        var isManager = User.IsInRole("Manager");
        if (!isManager && application.CreatedByUserId != CurrentUserId
            && application.AssignedOfficerUserId != CurrentUserId)
            throw new UnauthorizedAccessException("Bu başvuruyu geri çekme yetkiniz yok.");
        var previous = application.Status;
        var next = ApplicationOperationsPolicy.DetermineCancellationStatus(previous,
            application.CreditCard is not null, isManager, request.Reason);
        application.Status = next;
        application.CancelledByUserId = CurrentUserId;
        application.CancelledAtUtc = DateTime.UtcNow;
        application.CancellationReason = request.Reason.Trim();
        application.FirstApprovedByUserId = null;
        application.FirstApprovedAtUtc = null;
        application.FirstApprovalNote = null;
        application.Histories.Add(new ApplicationHistory
        {
            PreviousStatus = previous, NewStatus = next, ChangedByUserId = CurrentUserId,
            Description = $"{(isManager ? "Operasyonel iptal" : "Memur geri çekme")}: {application.CancellationReason}"
        });
        var recipients = await db.Users.Where(x => x.IsActive && x.NotifyApplicationEvents
            && (x.Id == application.CreatedByUserId || x.Id == application.AssignedOfficerUserId
                || (isManager == false && x.UserRoles.Any(r => r.Role.Name == "Manager"))))
            .Select(x => x.Id).Distinct().ToListAsync(cancellationToken);
        foreach (var userId in recipients.Where(x => x != CurrentUserId))
            db.Notifications.Add(new Notification
            {
                UserId = userId, Type = "Application", Title = isManager ? "Başvuru iptal edildi" : "Başvuru geri çekildi",
                Message = $"{application.ApplicationNumber}: {application.CancellationReason}",
                Link = $"/{(User.IsInRole("Manager") ? "manager" : "officer")}/applications/{application.Id}"
            });
        await db.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync(CurrentUserId, isManager ? "ApplicationCancelled" : "ApplicationWithdrawn",
            "CardApplication", applicationId.ToString(), application.CancellationReason, RemoteIp, cancellationToken);
        return NoContent();
    }

    [Authorize(Roles = "Manager")]
    [HttpGet("quality")]
    public async Task<ActionResult<DecisionQualityResponse>> GetQuality(
        [FromQuery] int days = 30, CancellationToken cancellationToken = default)
    {
        days = Math.Clamp(days, 1, 365);
        var since = DateTime.UtcNow.AddDays(-days);
        var decisions = await db.ApplicationHistories.AsNoTracking().Include(x => x.ChangedByUser)
            .Include(x => x.CardApplication)
            .Where(x => x.CreatedAtUtc >= since && (x.NewStatus == ApplicationStatus.Approved
                || x.NewStatus == ApplicationStatus.Rejected || x.NewStatus == ApplicationStatus.Revision))
            .ToListAsync(cancellationToken);
        var total = decisions.Count;
        decimal Rate(ApplicationStatus status) => total == 0 ? 0
            : Math.Round(decisions.Count(x => x.NewStatus == status) * 100m / total, 1);
        double Minutes(ApplicationHistory item) => Math.Max(0,
            (item.CreatedAtUtc - item.CardApplication.CreatedAtUtc).TotalMinutes);
        var managers = decisions.GroupBy(x => new { x.ChangedByUserId, x.ChangedByUser.FullName })
            .Select(group => new DecisionQualityManagerItem(group.Key.ChangedByUserId, group.Key.FullName,
                group.Count(), group.Count(x => x.NewStatus == ApplicationStatus.Approved),
                group.Count(x => x.NewStatus == ApplicationStatus.Rejected),
                group.Count(x => x.NewStatus == ApplicationStatus.Revision),
                group.Count() == 0 ? 0 : Math.Round(group.Count(x => x.NewStatus == ApplicationStatus.Approved) * 100m / group.Count(), 1),
                Math.Round(group.Average(Minutes), 1)))
            .OrderByDescending(x => x.Total).ToList();
        var open = await db.CardApplications.AsNoTracking().Include(x => x.Histories)
            .Where(x => x.Status == ApplicationStatus.Pending || x.Status == ApplicationStatus.Revision)
            .ToListAsync(cancellationToken);
        var overdue = open.Count(x =>
            (DateTime.UtcNow - ApplicationOperationsPolicy.GetCurrentStageStartedAt(x)).TotalHours >= 8);
        var dual = await db.CardApplications.AsNoTracking().CountAsync(x => x.FirstApprovedAtUtc >= since, cancellationToken);
        var reassigned = await db.AuditLogs.AsNoTracking().CountAsync(x => x.CreatedAtUtc >= since
            && x.Action == "ApplicationReassigned", cancellationToken);
        return Ok(new DecisionQualityResponse(days, total, Rate(ApplicationStatus.Approved),
            Rate(ApplicationStatus.Rejected), Rate(ApplicationStatus.Revision),
            total == 0 ? 0 : Math.Round(decisions.Average(Minutes), 1), dual, overdue, reassigned, managers));
    }

    [Authorize(Roles = "Manager")]
    [HttpGet("audit")]
    public async Task<ActionResult<IReadOnlyList<AuditLogResponse>>> GetAudit(
        [FromQuery] string? search, [FromQuery] string? action,
        [FromQuery] int limit = 200, CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 500);
        var query = db.AuditLogs.AsNoTracking().Include(x => x.User).AsQueryable();
        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(x => x.Action == action);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim();
            query = query.Where(x => x.Action.Contains(value) || x.EntityName.Contains(value)
                || (x.EntityId != null && x.EntityId.Contains(value))
                || (x.Detail != null && x.Detail.Contains(value)));
        }
        var result = await query.OrderByDescending(x => x.CreatedAtUtc).Take(limit)
            .Select(x => new AuditLogResponse(x.Id, x.Action, x.EntityName, x.EntityId,
                x.Detail, x.User == null ? null : x.User.FullName, x.IpAddress, x.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        return Ok(result);
    }

    private static void Add(List<KycCheckItem> checks, string key, string label,
        bool passed, string detail, bool blocking) => checks.Add(new KycCheckItem(
            key, label, passed ? "Passed" : "Failed", detail, blocking));

    private void EnsureApplicationAccess(CardApplication application)
    {
        if (User.IsInRole("Manager")) return;
        if (application.CreatedByUserId != CurrentUserId && application.AssignedOfficerUserId != CurrentUserId)
            throw new UnauthorizedAccessException("Bu başvurunun KYC sonucuna erişim yetkiniz yok.");
    }

    private int CurrentUserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new UnauthorizedAccessException("Geçerli kullanıcı bilgisi bulunamadı.");
    private string? RemoteIp => HttpContext.Connection.RemoteIpAddress?.ToString();
}
