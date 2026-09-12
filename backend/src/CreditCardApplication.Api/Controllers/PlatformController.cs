using System.Security.Claims;
using CreditCardApplication.Application.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CreditCardApplication.Api.Controllers;

[ApiController]
[Authorize(Roles = "Officer,Manager")]
[Route("api/platform")]
public sealed class PlatformController(IPlatformV2Service service) : ControllerBase
{
    [HttpGet("customers/{customerId:int}/kkb-analysis")]
    public async Task<IActionResult> GetKkb(int customerId, CancellationToken cancellationToken) =>
        Ok(await service.GetKkbAnalysisAsync(customerId, cancellationToken));

    [HttpGet("sla")]
    [Authorize(Roles = "Manager")]
    public async Task<IActionResult> GetSla(CancellationToken cancellationToken) =>
        Ok(await service.GetSlaAsync(cancellationToken));

    [HttpGet("notifications")]
    public async Task<IActionResult> GetNotifications(CancellationToken cancellationToken) =>
        Ok(await service.GetNotificationsAsync(UserId, cancellationToken));

    [HttpPost("notifications/{id:int}/read")]
    public async Task<IActionResult> ReadNotification(int id, CancellationToken cancellationToken)
    {
        await service.MarkNotificationReadAsync(id, UserId, cancellationToken);
        return NoContent();
    }

    [HttpPost("notifications/read-all")]
    public async Task<IActionResult> ReadAllNotifications(CancellationToken cancellationToken)
    {
        await service.MarkAllNotificationsReadAsync(UserId, cancellationToken);
        return NoContent();
    }

    [HttpGet("chat/users")]
    public async Task<IActionResult> GetChatUsers(CancellationToken cancellationToken) =>
        Ok(await service.GetChatUsersAsync(UserId, cancellationToken));

    [HttpGet("chat/{otherUserId:int}")]
    public async Task<IActionResult> GetConversation(int otherUserId, CancellationToken cancellationToken) =>
        Ok(await service.GetConversationAsync(UserId, otherUserId, cancellationToken));

    [HttpPost("chat")]
    public async Task<IActionResult> SendMessage(
        [FromBody] SendChatMessageRequest request, CancellationToken cancellationToken) =>
        Ok(await service.SendMessageAsync(UserId, request, cancellationToken));

    [HttpPost("chat/bulk")]
    public async Task<IActionResult> SendBulkMessage(
        [FromBody] SendBulkChatMessageRequest request, CancellationToken cancellationToken) =>
        Ok(await service.SendBulkMessageAsync(UserId, request, cancellationToken));

    [HttpPost("chat/{messageId:int}/report")]
    public async Task<IActionResult> ReportMessage(
        int messageId, [FromBody] ReportChatMessageRequest request, CancellationToken cancellationToken)
    {
        await service.ReportChatMessageAsync(UserId, messageId, request, cancellationToken);
        return NoContent();
    }

    [HttpGet("customers/{customerId:int}/consents")]
    public async Task<IActionResult> GetConsents(int customerId, CancellationToken cancellationToken) =>
        Ok(await service.GetConsentsAsync(customerId, cancellationToken));

    [HttpPost("customers/{customerId:int}/consents")]
    public async Task<IActionResult> SetConsent(
        int customerId, [FromBody] ConsentRequest request, CancellationToken cancellationToken) =>
        Ok(await service.SetConsentAsync(customerId, request, UserId, cancellationToken));

    [HttpPost("simulation")]
    public async Task<IActionResult> Simulate(
        [FromBody] SimulationRequest request, CancellationToken cancellationToken) =>
        Ok(await service.SimulateAsync(request, cancellationToken));

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken) =>
        Ok(await service.GetProfileAsync(UserId, cancellationToken));

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateProfilePreferencesRequest request, CancellationToken cancellationToken) =>
        Ok(await service.UpdateProfileAsync(UserId, request, cancellationToken));

    private int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new UnauthorizedAccessException("Geçerli kullanıcı bulunamadı.");
}
