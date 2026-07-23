using System.Security.Claims;
using CreditCardApplication.Application.Cards;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CreditCardApplication.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/cards")]
public sealed class CardsController(CreditCardService creditCardService) : ControllerBase
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
}
