using System.Security.Claims;
using CreditCardApplication.Application.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CreditCardApplication.Api.Controllers;

[ApiController]
[Authorize(Roles = "Officer,Manager")]
[Route("api/supplementary-card-applications")]
public sealed class SupplementaryCardApplicationsController(IPlatformV2Service service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        Ok(await service.GetSupplementaryAsync(User.IsInRole("Manager"), UserId, cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken) =>
        Ok(await service.GetSupplementaryByIdAsync(id, User.IsInRole("Manager"), UserId, cancellationToken));

    [HttpPost]
    [Authorize(Roles = "Officer")]
    public async Task<IActionResult> Create(
        [FromBody] CreateSupplementaryApplicationRequest request, CancellationToken cancellationToken) =>
        Ok(await service.CreateSupplementaryAsync(request, UserId, cancellationToken));

    [HttpGet("card-context/{primaryCardId:int}")]
    [Authorize(Roles = "Officer")]
    public async Task<IActionResult> GetCardContext(
        int primaryCardId, CancellationToken cancellationToken) =>
        Ok(await service.GetSupplementaryCardContextAsync(primaryCardId, UserId, cancellationToken));

    [HttpPost("{id:int}/evaluation")]
    [Authorize(Roles = "Manager")]
    public async Task<IActionResult> Evaluate(
        int id, [FromBody] EvaluateSupplementaryApplicationRequest request, CancellationToken cancellationToken) =>
        Ok(await service.EvaluateSupplementaryAsync(id, request, UserId, cancellationToken));

    private int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new UnauthorizedAccessException("Geçerli kullanıcı bulunamadı.");
}
