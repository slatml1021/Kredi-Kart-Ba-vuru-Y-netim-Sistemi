using CreditCardApplication.Application.Applications;
using CreditCardApplication.Application.Auditing;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CreditCardApplication.Api.Controllers;

[ApiController]
[Route("api/card-applications")]
public sealed class CardApplicationsController(CardApplicationService applicationService, AuditService auditService) : ControllerBase
{
    [Authorize(Roles = "Officer")]
    [HttpGet("mine")]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken) =>
        Ok(await applicationService.GetByOfficerAsync(GetCurrentUserId(), cancellationToken));

    [Authorize(Roles = "Manager")]
    [HttpGet("pending")]
    public async Task<IActionResult> GetPending(CancellationToken cancellationToken) =>
        Ok(await applicationService.GetPendingAsync(cancellationToken));

    [Authorize]
    [HttpGet("card-types")]
    public async Task<IActionResult> GetCardTypes(CancellationToken cancellationToken) =>
        Ok(await applicationService.GetCardTypesAsync(cancellationToken));

    [Authorize]
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        if (User.IsInRole("Officer") && !await applicationService.IsOwnedByAsync(id, GetCurrentUserId(), cancellationToken))
            return Forbid();
        var application = await applicationService.GetByIdAsync(id, cancellationToken);
        return application is null ? NotFound() : Ok(application);
    }

    [Authorize]
    [HttpGet("{id:int}/detail")]
    public async Task<IActionResult> GetDetailById(int id, CancellationToken cancellationToken)
    {
        if (User.IsInRole("Officer") && !await applicationService.IsOwnedByAsync(id, GetCurrentUserId(), cancellationToken))
            return Forbid();
        var application = await applicationService.GetDetailByIdAsync(id, cancellationToken);
        return application is null ? NotFound() : Ok(application);
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
        await WriteAuditAsync("ApplicationResubmitted", application, cancellationToken);
        return Ok(application);
    }

    private int GetCurrentUserId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            ? userId
            : throw new UnauthorizedAccessException("Geçerli kullanıcı bilgisi bulunamadı.");

    private Task WriteAuditAsync(string action, CardApplicationResponse application, CancellationToken cancellationToken) =>
        auditService.WriteAsync(GetCurrentUserId(), action, "CardApplication", application.Id.ToString(),
            application.ApplicationNumber, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
}
