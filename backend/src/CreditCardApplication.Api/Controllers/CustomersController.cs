using CreditCardApplication.Application.Customers;
using CreditCardApplication.Application.Auditing;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CreditCardApplication.Api.Controllers;

[ApiController]
[Route("api/customers")]
public sealed class CustomersController(CustomerService customerService, AuditService auditService) : ControllerBase
{
    [Authorize(Roles = "Officer")]
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var customer = await customerService.GetByIdAsync(id, cancellationToken);
        return customer is null ? NotFound() : Ok(customer);
    }

    [HttpGet]
    [Authorize(Roles = "Officer")]
    public async Task<IActionResult> Search([FromQuery] string query, CancellationToken cancellationToken)
    {
        var customers = await customerService.SearchAsync(query, cancellationToken);
        return Ok(customers);
    }

    [HttpPost]
    [Authorize(Roles = "Officer")]
    public async Task<IActionResult> Create(
        [FromBody] CreateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var customer = await customerService.CreateAsync(request, cancellationToken);
        await auditService.WriteAsync(GetCurrentUserId(), "CustomerCreated", "Customer", customer.Id.ToString(),
            customer.CustomerNumber, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = customer.Id }, customer);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Officer")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCustomerRequest request, CancellationToken cancellationToken)
    {
        var customer = await customerService.UpdateAsync(id, request, cancellationToken);
        await auditService.WriteAsync(GetCurrentUserId(), "CustomerUpdated", "Customer", id.ToString(),
            customer.CustomerNumber, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return Ok(customer);
    }

    private int GetCurrentUserId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            ? userId
            : throw new UnauthorizedAccessException("Geçerli kullanıcı bilgisi bulunamadı.");
}
