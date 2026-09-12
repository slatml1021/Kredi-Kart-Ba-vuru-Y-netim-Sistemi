using CreditCardApplication.Application.Customers;
using CreditCardApplication.Application.Auditing;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CreditCardApplication.Application.Platform;

namespace CreditCardApplication.Api.Controllers;

[ApiController]
[Route("api/customers")]
public sealed class CustomersController(
    CustomerService customerService,
    AuditService auditService,
    IPlatformV2Service platformService) : ControllerBase
{
    [Authorize(Roles = "Officer,Manager")]
    [HttpGet("list")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        Ok(await customerService.GetAllAsync(cancellationToken));

    [Authorize(Roles = "Officer")]
    [HttpGet("external-risk-profile")]
    public IActionResult GetExternalRiskProfile([FromQuery] string nationalIdentityNumber) =>
        Ok(customerService.GetExternalRiskProfile(nationalIdentityNumber));

    [Authorize(Roles = "Officer,Manager")]
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var customer = await customerService.GetByIdAsync(id, cancellationToken);
        return customer is null ? NotFound() : Ok(customer);
    }

    [Authorize(Roles = "Officer,Manager")]
    [HttpGet("{id:int}/detail")]
    public async Task<IActionResult> GetDetailById(int id, CancellationToken cancellationToken)
    {
        var customer = await customerService.GetDetailByIdAsync(id, cancellationToken);
        return customer is null ? NotFound() : Ok(customer);
    }

    [HttpGet]
    [Authorize(Roles = "Officer,Manager")]
    public async Task<IActionResult> Search(
        [FromQuery] string query,
        [FromQuery] string? criterion,
        CancellationToken cancellationToken)
    {
        var customers = await customerService.SearchAsync(query, criterion, cancellationToken);
        return Ok(customers);
    }

    [HttpPost]
    [Authorize(Roles = "Officer")]
    public async Task<IActionResult> Create(
        [FromBody] CreateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var customer = await customerService.CreateAsync(request, cancellationToken);
        var userId = GetCurrentUserId();
        await platformService.SetConsentAsync(customer.Id,
            new ConsentRequest("KVKK", "v2.1", request.KvkkConsentGranted, "Şube"), userId, cancellationToken);
        await platformService.SetConsentAsync(customer.Id,
            new ConsentRequest("SMS", "v2.1", request.SmsConsentGranted, "Şube"), userId, cancellationToken);
        await platformService.SetConsentAsync(customer.Id,
            new ConsentRequest("EMAIL", "v2.1", request.EmailConsentGranted, "Şube"), userId, cancellationToken);
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

    [HttpPost("{id:int}/verify-contact")]
    [Authorize(Roles = "Officer")]
    public async Task<IActionResult> VerifyContact(
        int id,
        [FromBody] VerifyCustomerContactRequest request,
        CancellationToken cancellationToken)
    {
        var customer = await customerService.VerifyContactAsync(id, request, cancellationToken);
        await auditService.WriteAsync(GetCurrentUserId(), "CustomerContactVerified", "Customer", id.ToString(),
            request.Channel, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return Ok(customer);
    }

    [HttpPost("{id:int}/request-contact-verification")]
    [Authorize(Roles = "Officer")]
    public async Task<IActionResult> RequestContactVerification(
        int id,
        [FromBody] RequestCustomerContactVerificationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await customerService.RequestContactVerificationAsync(id, request, cancellationToken);
        await auditService.WriteAsync(GetCurrentUserId(), "CustomerContactVerificationRequested", "Customer",
            id.ToString(), request.Channel, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return Ok(result);
    }

    [HttpPatch("{id:int}/status")]
    [Authorize(Roles = "Officer")]
    public async Task<IActionResult> SetStatus(
        int id,
        [FromBody] SetCustomerStatusRequest request,
        CancellationToken cancellationToken)
    {
        var customer = await customerService.SetStatusAsync(id, request, cancellationToken);
        await auditService.WriteAsync(GetCurrentUserId(), request.IsActive ? "CustomerActivated" : "CustomerDeactivated",
            "Customer", id.ToString(), customer.CustomerNumber,
            HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return Ok(customer);
    }

    [HttpGet("{id:int}/addresses")]
    [Authorize(Roles = "Officer,Manager")]
    public async Task<IActionResult> GetAddresses(int id, CancellationToken cancellationToken) =>
        Ok(await customerService.GetAddressesAsync(id, cancellationToken));

    [HttpPost("{id:int}/addresses")]
    [Authorize(Roles = "Officer")]
    public async Task<IActionResult> AddAddress(
        int id, [FromBody] CustomerAddressRequest request, CancellationToken cancellationToken)
    {
        var address = await customerService.AddAddressAsync(id, request, cancellationToken);
        await auditService.WriteAsync(GetCurrentUserId(), "CustomerAddressCreated", "CustomerAddress",
            address.Id.ToString(), $"{address.Name} - {address.FullAddress}",
            HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return Ok(address);
    }

    [HttpPut("{id:int}/addresses/{addressId:int}")]
    [Authorize(Roles = "Officer")]
    public async Task<IActionResult> UpdateAddress(
        int id, int addressId, [FromBody] CustomerAddressRequest request, CancellationToken cancellationToken) =>
        Ok(await customerService.UpdateAddressAsync(id, addressId, request, cancellationToken));

    [HttpPatch("{id:int}/addresses/{addressId:int}/default")]
    [Authorize(Roles = "Officer")]
    public async Task<IActionResult> SetDefaultAddress(
        int id, int addressId, CancellationToken cancellationToken) =>
        Ok(await customerService.SetDefaultAddressAsync(id, addressId, cancellationToken));

    [HttpDelete("{id:int}/addresses/{addressId:int}")]
    [Authorize(Roles = "Officer")]
    public async Task<IActionResult> DeleteAddress(
        int id, int addressId, CancellationToken cancellationToken)
    {
        await customerService.DeleteAddressAsync(id, addressId, cancellationToken);
        return NoContent();
    }

    private int GetCurrentUserId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            ? userId
            : throw new UnauthorizedAccessException("Geçerli kullanıcı bilgisi bulunamadı.");
}
