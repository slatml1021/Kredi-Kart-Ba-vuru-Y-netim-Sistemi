using System.Security.Claims;
using CreditCardApplication.Application.Cards;
using CreditCardApplication.Application.Auditing;
using CreditCardApplication.Application.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CreditCardApplication.Api.Controllers;

[ApiController]
[Authorize(Roles = "Officer,Manager")]
[Route("api/cards")]
public sealed class CardsController(CreditCardService creditCardService, IPlatformV2Service platformService,
    AuditService auditService) : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        int? officerId = null;
        if (User.IsInRole("Officer"))
            officerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var card = await creditCardService.GetByIdAsync(id, officerId, cancellationToken);
        return card is null ? NotFound() : Ok(card);
    }

    [HttpPost("{id:int}/limit-change")]
    [Authorize(Roles = "Officer")]
    public async Task<IActionResult> RequestLimitChange(
        int id,
        [FromBody] CreateLimitChangeRequest request,
        CancellationToken cancellationToken)
    {
        var officerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await creditCardService.RequestLimitChangeAsync(id, request, officerId, cancellationToken);
        if (request.ChangeType.Equals("Decrease", StringComparison.OrdinalIgnoreCase))
            await platformService.NotifyLimitDecreaseAsync(id, officerId, request.RequestedNewLimit, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}/fulfillment")]
    public async Task<IActionResult> GetFulfillment(int id, CancellationToken cancellationToken)
    {
        if (!await CanAccessCardAsync(id, cancellationToken)) return Forbid();
        var result = await platformService.GetFulfillmentAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("{id:int}/pdf")]
    public async Task<IActionResult> GetPdf(
        int id, CancellationToken cancellationToken, [FromQuery] bool email = false)
    {
        if (!await CanAccessCardAsync(id, cancellationToken)) return Forbid();
        var result = await platformService.GenerateCardPdfAsync(id, email, cancellationToken);
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await auditService.WriteAsync(userId, email ? "CardPdfEmailed" : "CardPdfDownloaded",
            "CreditCard", id.ToString(), email ? "Kart PDF'i müşteriye e-postalandı." : "Kart PDF'i indirildi.",
            HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return File(result.Content, "application/pdf", result.FileName);
    }

    [HttpPost("{id:int}/limit-increase")]
    [Authorize(Roles = "Officer")]
    public async Task<IActionResult> RequestLimitIncrease(
        int id,
        [FromBody] CreateLimitIncreaseRequest request,
        CancellationToken cancellationToken)
    {
        var officerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return Ok(await creditCardService.RequestLimitIncreaseAsync(id, request, officerId, cancellationToken));
    }

    [HttpGet("limit-increases")]
    public async Task<IActionResult> GetLimitIncreases(CancellationToken cancellationToken)
    {
        int? officerId = User.IsInRole("Officer")
            ? int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)
            : null;
        return Ok(await creditCardService.GetLimitIncreaseRequestsAsync(officerId, cancellationToken));
    }

    [HttpPost("{id:int}/limit-increase/evaluate")]
    [Authorize(Roles = "Manager")]
    public async Task<IActionResult> EvaluateLimitIncrease(
        int id,
        [FromBody] EvaluateLimitIncreaseRequest request,
        CancellationToken cancellationToken)
    {
        var managerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return Ok(await creditCardService.EvaluateLimitIncreaseAsync(
            id, request, managerId, cancellationToken));
    }

    private async Task<bool> CanAccessCardAsync(int cardId, CancellationToken cancellationToken)
    {
        if (User.IsInRole("Manager")) return true;
        var officerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return await creditCardService.GetByIdAsync(cardId, officerId, cancellationToken) is not null;
    }
}
