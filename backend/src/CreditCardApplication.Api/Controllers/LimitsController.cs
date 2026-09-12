using CreditCardApplication.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CreditCardApplication.Api.Controllers;

[ApiController]
[Authorize(Roles = "Officer,Manager")]
[Route("api/limits")]
public sealed class LimitsController(LimitCalculator limitCalculator) : ControllerBase
{
    [HttpPost("calculate")]
    public IActionResult Calculate([FromBody] LimitCalculationRequest request)
    {
        var result = limitCalculator.Calculate(request.MonthlyNetIncome, request.OtherBankTotalCardLimit);
        return Ok(result);
    }
}

public sealed record LimitCalculationRequest(decimal MonthlyNetIncome, decimal OtherBankTotalCardLimit);
