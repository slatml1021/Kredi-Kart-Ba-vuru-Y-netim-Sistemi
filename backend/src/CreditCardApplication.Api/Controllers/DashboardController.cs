using System.Security.Claims;
using CreditCardApplication.Application.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CreditCardApplication.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/dashboard")]
public sealed class DashboardController(DashboardService dashboardService) : ControllerBase
{
    [HttpGet("officer")]
    [Authorize(Roles = "Officer")]
    public Task<OfficerDashboardResponse> Officer(CancellationToken cancellationToken) =>
        dashboardService.GetOfficerAsync(int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), cancellationToken);

    [HttpGet("manager")]
    [Authorize(Roles = "Manager")]
    public Task<ManagerDashboardResponse> Manager(
        [FromQuery] string period = "Week",
        [FromQuery] int? month = null,
        [FromQuery] int? year = null,
        CancellationToken cancellationToken = default) =>
        dashboardService.GetManagerAsync(period, month, year, cancellationToken);
}
