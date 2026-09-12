using CreditCardApplication.Application.Applications;
using CreditCardApplication.Application.Auditing;
using CreditCardApplication.Application.Platform;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CreditCardApplication.Api.Controllers;

[ApiController]
[Route("api/card-applications")]
public sealed class CardApplicationsController(
    CardApplicationService applicationService,
    AuditService auditService,
    IPlatformV2Service platformService) : ControllerBase
{
    [Authorize(Roles = "Officer")]
    [HttpGet("mine")]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken) =>
        Ok(await applicationService.GetByOfficerAsync(GetCurrentUserId(), cancellationToken));

    [Authorize(Roles = "Manager")]
    [HttpGet("pending")]
    public async Task<IActionResult> GetPending(CancellationToken cancellationToken) =>
        Ok(await applicationService.GetPendingAsync(cancellationToken));

    [Authorize(Roles = "Manager")]
    [HttpGet("all")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        Ok(await applicationService.GetAllAsync(cancellationToken));

    [Authorize(Roles = "Officer,Manager")]
    [HttpGet("card-types")]
    public async Task<IActionResult> GetCardTypes(CancellationToken cancellationToken) =>
        Ok(await applicationService.GetCardTypesAsync(cancellationToken));

    [Authorize(Roles = "Officer,Manager")]
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        if (User.IsInRole("Officer") && !await applicationService.IsOwnedByAsync(id, GetCurrentUserId(), cancellationToken))
            return Forbid();
        var application = await applicationService.GetByIdAsync(id, cancellationToken);
        return application is null ? NotFound() : Ok(application);
    }

    [Authorize(Roles = "Officer,Manager")]
    [HttpGet("{id:int}/pre-assessment")]
    public async Task<IActionResult> GetPreAssessment(int id, CancellationToken cancellationToken)
    {
        if (!await CanAccessApplicationAsync(id, cancellationToken)) return Forbid();
        return Ok(await platformService.GetPreAssessmentAsync(id, cancellationToken));
    }

    [Authorize(Roles = "Officer,Manager")]
    [HttpGet("duplicate-check")]
    public async Task<IActionResult> CheckDuplicate(
        [FromQuery] int customerId, [FromQuery] int cardTypeId, CancellationToken cancellationToken) =>
        Ok(await platformService.CheckDuplicateAsync(customerId, cardTypeId, cancellationToken));

    [Authorize(Roles = "Officer,Manager")]
    [HttpGet("{id:int}/revision-comparison")]
    public async Task<IActionResult> GetRevisionComparison(int id, CancellationToken cancellationToken)
    {
        if (!await CanAccessApplicationAsync(id, cancellationToken)) return Forbid();
        var result = await platformService.GetRevisionComparisonAsync(id, cancellationToken);
        return result is null ? NoContent() : Ok(result);
    }

    [Authorize(Roles = "Officer,Manager")]
    [HttpGet("{id:int}/pdf")]
    public async Task<IActionResult> GetPdf(
        int id, CancellationToken cancellationToken, [FromQuery] bool email = false)
    {
        if (!await CanAccessApplicationAsync(id, cancellationToken)) return Forbid();
        var result = await platformService.GenerateApplicationPdfAsync(id, email, cancellationToken);
        await auditService.WriteAsync(GetCurrentUserId(), email ? "ApplicationPdfEmailed" : "ApplicationPdfDownloaded",
            "CardApplication", id.ToString(), email ? "Başvuru PDF'i müşteriye e-postalandı." : "Başvuru PDF'i indirildi.",
            HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return File(result.Content, "application/pdf", result.FileName);
    }

    [Authorize(Roles = "Officer,Manager")]
    [HttpGet("{id:int}/detail")]
    public async Task<IActionResult> GetDetailById(int id, CancellationToken cancellationToken)
    {
        if (User.IsInRole("Officer") && !await applicationService.IsOwnedByAsync(id, GetCurrentUserId(), cancellationToken))
            return Forbid();
        var application = await applicationService.GetDetailByIdAsync(id, cancellationToken);
        return application is null ? NotFound() : Ok(application);
    }

    [Authorize(Roles = "Officer,Manager")]
    [HttpPost("{id:int}/documents")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> UploadDocument(
        int id, [FromForm] string documentType, [FromForm] IFormFile file,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessApplicationAsync(id, cancellationToken)) return Forbid();
        if (file.Length == 0) return BadRequest("Boş belge yüklenemez.");
        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        return Ok(await platformService.UploadApplicationDocumentAsync(
            id, documentType, file.FileName, stream.ToArray(), GetCurrentUserId(), cancellationToken));
    }

    [Authorize(Roles = "Officer,Manager")]
    [HttpGet("{id:int}/documents")]
    public async Task<IActionResult> GetDocuments(int id, CancellationToken cancellationToken)
    {
        if (!await CanAccessApplicationAsync(id, cancellationToken)) return Forbid();
        return Ok(await platformService.GetApplicationDocumentsAsync(id, GetCurrentUserId(), cancellationToken));
    }

    [Authorize(Roles = "Officer,Manager")]
    [HttpGet("{id:int}/documents/{documentId:int}")]
    public async Task<IActionResult> DownloadDocument(int id, int documentId, CancellationToken cancellationToken)
    {
        if (!await CanAccessApplicationAsync(id, cancellationToken)) return Forbid();
        var document = await platformService.GetApplicationDocumentAsync(
            id, documentId, GetCurrentUserId(), cancellationToken);
        await auditService.WriteAsync(GetCurrentUserId(), "ApplicationDocumentDownloaded", "GeneratedDocument",
            documentId.ToString(), $"Başvuru #{id}; {document.FileName}",
            HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return File(document.Content, document.ContentType, document.FileName);
    }

    [Authorize(Roles = "Officer")]
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateCardApplicationRequest request,
        CancellationToken cancellationToken)
    {
        var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId)
            ? parsedUserId
            : throw new UnauthorizedAccessException("Geçerli kullanıcı bilgisi bulunamadı.");
        var application = await applicationService.CreateAsync(request, userId, cancellationToken);
        await platformService.GetPreAssessmentAsync(application.Id, cancellationToken);
        await platformService.GenerateApplicationPdfAsync(application.Id, true, cancellationToken);
        await platformService.NotifyApplicationEventAsync(application.Id, "Created", cancellationToken);
        application = await applicationService.GetByIdAsync(application.Id, cancellationToken) ?? application;
        await WriteAuditAsync("ApplicationCreated", application, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = application.Id }, application);
    }

    [Authorize(Roles = "Manager")]
    [HttpPost("{id:int}/evaluation")]
    public async Task<IActionResult> Evaluate(
        int id,
        [FromBody] EvaluateCardApplicationRequest request,
        CancellationToken cancellationToken)
    {
        var managerUserId = GetCurrentUserId();
        var application = await applicationService.EvaluateAsync(id, request, managerUserId, cancellationToken);
        if (application.Status == "Approved")
        {
            var detail = await applicationService.GetDetailByIdAsync(id, cancellationToken);
            if (detail?.CreditCardId is int cardId)
            {
                await platformService.GetFulfillmentAsync(cardId, cancellationToken);
                await platformService.GenerateCardPdfAsync(cardId, true, cancellationToken);
            }
        }
        await platformService.NotifyApplicationEventAsync(application.Id,
            application.WorkflowStage == "SecondManagerApproval" ? "FirstApproval" : application.Status,
            cancellationToken);
        await WriteAuditAsync($"Application{application.Status}", application, cancellationToken);
        return Ok(application);
    }

    [Authorize(Roles = "Officer")]
    [HttpPost("{id:int}/resubmit")]
    public async Task<IActionResult> Resubmit(
        int id,
        [FromBody] ResubmitCardApplicationRequest request,
        CancellationToken cancellationToken)
    {
        var application = await applicationService.ResubmitAsync(id, request, GetCurrentUserId(), cancellationToken);
        await platformService.NotifyApplicationEventAsync(application.Id, "Resubmitted", cancellationToken);
        await WriteAuditAsync("ApplicationResubmitted", application, cancellationToken);
        return Ok(application);
    }

    private int GetCurrentUserId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            ? userId
            : throw new UnauthorizedAccessException("Geçerli kullanıcı bilgisi bulunamadı.");

    private Task<bool> CanAccessApplicationAsync(int applicationId, CancellationToken cancellationToken) =>
        User.IsInRole("Manager")
            ? Task.FromResult(true)
            : applicationService.IsOwnedByAsync(applicationId, GetCurrentUserId(), cancellationToken);

    private Task WriteAuditAsync(string action, CardApplicationResponse application, CancellationToken cancellationToken) =>
        auditService.WriteAsync(GetCurrentUserId(), action, "CardApplication", application.Id.ToString(),
            application.ApplicationNumber, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
}
