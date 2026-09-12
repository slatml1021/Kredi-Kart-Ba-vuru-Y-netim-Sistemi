using System.Security.Claims;
using CreditCardApplication.Api.Auth;
using CreditCardApplication.Application.Platform;
using CreditCardApplication.Application.Customers;
using CreditCardApplication.Domain.Enums;
using CreditCardApplication.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace CreditCardApplication.Api.Controllers;

public sealed record CustomerPortalLoginRequest(string CustomerNumber, string Password);
public sealed record CustomerSimulationRequest(int CardTypeId, decimal RequestedLimit);
public sealed record ActivateCustomerCardRequest(string VerificationCode);

[ApiController]
[Route("api/customer-portal")]
public sealed class CustomerPortalController(
    ApplicationDbContext db,
    JwtTokenService tokenService,
    IPlatformV2Service platformService,
    IContactVerificationSender verificationSender) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(CustomerPortalLoginRequest request, CancellationToken cancellationToken)
    {
        var username = request.CustomerNumber.Trim().ToUpperInvariant();
        var account = await db.CustomerAccounts.Include(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Username == username, cancellationToken)
            ?? throw new UnauthorizedAccessException("Müşteri numarası veya şifre hatalı.");
        if (!account.IsActive || !account.Customer.IsActive)
            throw new UnauthorizedAccessException("Müşteri portal hesabı aktif değil.");
        if (account.LockoutEndUtc > DateTime.UtcNow)
            throw new UnauthorizedAccessException("Çok fazla hatalı giriş nedeniyle hesap geçici olarak kilitlendi.");
        if (!BCrypt.Net.BCrypt.Verify(request.Password, account.PasswordHash))
        {
            account.FailedLoginCount++;
            if (account.FailedLoginCount >= 5) account.LockoutEndUtc = DateTime.UtcNow.AddMinutes(15);
            await db.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedAccessException("Müşteri numarası veya şifre hatalı.");
        }
        account.FailedLoginCount = 0;
        account.LockoutEndUtc = null;
        account.LastLoginAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(tokenService.CreateCustomer(account.Id, account.CustomerId, account.Customer.CustomerNumber,
            $"{account.Customer.FirstName} {account.Customer.LastName}"));
    }

    [Authorize(Roles = "Customer")]
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken cancellationToken)
    {
        var customerId = CustomerId;
        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == customerId, cancellationToken)
            ?? throw new ArgumentException("Müşteri bulunamadı.");
        var applications = await db.CardApplications.AsNoTracking().Include(x => x.CardType)
            .Where(x => x.CustomerId == customerId).OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new { x.Id, x.ApplicationNumber, CardType = x.CardType.Name, x.RequestedLimit, Status = x.Status.ToString(), x.CreatedAtUtc })
            .ToListAsync(cancellationToken);
        var cards = await db.CreditCards.AsNoTracking().Include(x => x.CardApplication).ThenInclude(x => x.CardType)
            .Where(x => x.CardApplication.CustomerId == customerId)
            .Select(x => new
            {
                x.Id, x.MaskedCardNumber, CardType = x.CardApplication.CardType.Name, x.CardLimit,
                Status = x.Status.ToString(),
                FulfillmentStatus = x.Fulfillment == null ? null : x.Fulfillment.Status,
                EstimatedDeliveryAtUtc = x.Fulfillment == null ? (DateTime?)null : x.Fulfillment.EstimatedDeliveryAtUtc
            })
            .ToListAsync(cancellationToken);
        var supplementaryCards = await db.SupplementaryCardApplications.AsNoTracking()
            .Include(x => x.PrimaryCustomer)
            .Where(x => x.SupplementaryHolderCustomerId == customerId && x.Status == "Approved")
            .Select(x => new
            {
                x.Id, x.ApplicationNumber, x.MaskedCardNumber, x.RequestedLimit,
                x.CardStatus, x.FulfillmentStatus, x.EstimatedDeliveryAtUtc,
                PrimaryCardHolder = x.PrimaryCustomer.FirstName + " " + x.PrimaryCustomer.LastName
            }).ToListAsync(cancellationToken);
        return Ok(new { Customer = new { customer.CustomerNumber, FullName = $"{customer.FirstName} {customer.LastName}" }, Applications = applications, Cards = cards, SupplementaryCards = supplementaryCards });
    }

    [Authorize(Roles = "Customer")]
    [HttpGet("cards/{id:int}/fulfillment")]
    public async Task<IActionResult> GetCardFulfillment(int id, CancellationToken cancellationToken)
    {
        var ownsCard = await db.CreditCards.AsNoTracking()
            .AnyAsync(x => x.Id == id && x.CardApplication.CustomerId == CustomerId, cancellationToken);
        if (!ownsCard) return NotFound();
        var fulfillment = await platformService.GetFulfillmentAsync(id, cancellationToken);
        return fulfillment is null ? NotFound() : Ok(fulfillment);
    }

    [Authorize(Roles = "Customer")]
    [HttpPost("cards/{id:int}/activation-code")]
    public async Task<IActionResult> RequestCardActivationCode(int id, CancellationToken cancellationToken)
    {
        var card = await db.CreditCards.AsNoTracking().Include(x => x.CardApplication).ThenInclude(x => x.Customer)
            .Include(x => x.Fulfillment)
            .FirstOrDefaultAsync(x => x.Id == id && x.CardApplication.CustomerId == CustomerId, cancellationToken)
            ?? throw new ArgumentException("Kart bulunamadı.");
        if (card.Fulfillment?.Status != "Delivered" || card.Status != CardStatus.Inactive)
            throw new InvalidOperationException("Aktivasyon kodu yalnızca teslim edilmiş pasif kart için gönderilebilir.");
        var code = ActivationCode(card.Id, card.CardApplicationId);
        var (channel, destination) = ActivationDestination(card.CardApplication.Customer);
        await verificationSender.SendAsync(channel, destination, code, cancellationToken);
        return Ok(new { Channel = channel, MaskedDestination = MaskDestination(destination), DemoCode = verificationSender.ExposeDemoCode ? code : null });
    }

    [Authorize(Roles = "Customer")]
    [HttpPost("cards/{id:int}/activate")]
    public async Task<IActionResult> ActivateCard(int id, ActivateCustomerCardRequest request, CancellationToken cancellationToken)
    {
        var card = await db.CreditCards.Include(x => x.CardApplication).Include(x => x.Fulfillment)
            .FirstOrDefaultAsync(x => x.Id == id && x.CardApplication.CustomerId == CustomerId, cancellationToken)
            ?? throw new ArgumentException("Kart bulunamadı.");
        if (card.Fulfillment?.Status != "Delivered")
            throw new InvalidOperationException("Kart teslim edilmeden aktive edilemez.");
        if (card.Status != CardStatus.Inactive)
            throw new InvalidOperationException("Yalnızca pasif kart aktive edilebilir.");
        if (!string.Equals(request.VerificationCode?.Trim(), ActivationCode(card.Id, card.CardApplicationId), StringComparison.Ordinal))
            throw new ArgumentException("Aktivasyon kodu geçersizdir.");
        card.Status = CardStatus.Active;
        card.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { card.Id, Status = card.Status.ToString(), ActivatedAtUtc = DateTime.UtcNow });
    }

    [Authorize(Roles = "Customer")]
    [HttpPost("supplementary-cards/{id:int}/activation-code")]
    public async Task<IActionResult> RequestSupplementaryActivationCode(int id, CancellationToken cancellationToken)
    {
        var card = await db.SupplementaryCardApplications.AsNoTracking()
            .Include(x => x.SupplementaryHolderCustomer)
            .FirstOrDefaultAsync(x => x.Id == id && x.SupplementaryHolderCustomerId == CustomerId, cancellationToken)
            ?? throw new ArgumentException("Ek kart bulunamadı.");
        if (card.FulfillmentStatus != "Delivered" || card.CardStatus != "Inactive")
            throw new InvalidOperationException("Aktivasyon kodu yalnızca teslim edilmiş pasif ek kart için gönderilebilir.");
        var code = ActivationCode(card.Id, card.PrimaryCreditCardId);
        var (channel, destination) = ActivationDestination(card.SupplementaryHolderCustomer);
        await verificationSender.SendAsync(channel, destination, code, cancellationToken);
        return Ok(new { Channel = channel, MaskedDestination = MaskDestination(destination), DemoCode = verificationSender.ExposeDemoCode ? code : null });
    }

    [Authorize(Roles = "Customer")]
    [HttpPost("supplementary-cards/{id:int}/activate")]
    public async Task<IActionResult> ActivateSupplementaryCard(int id, ActivateCustomerCardRequest request, CancellationToken cancellationToken)
    {
        var card = await db.SupplementaryCardApplications.FirstOrDefaultAsync(
            x => x.Id == id && x.SupplementaryHolderCustomerId == CustomerId, cancellationToken)
            ?? throw new ArgumentException("Ek kart bulunamadı.");
        if (card.FulfillmentStatus != "Delivered")
            throw new InvalidOperationException("Ek kart teslim edilmeden aktive edilemez.");
        if (card.CardStatus != "Inactive")
            throw new InvalidOperationException("Yalnızca pasif ek kart aktive edilebilir.");
        if (!string.Equals(request.VerificationCode?.Trim(), ActivationCode(card.Id, card.PrimaryCreditCardId), StringComparison.Ordinal))
            throw new ArgumentException("Aktivasyon kodu geçersizdir.");
        card.CardStatus = "Active";
        card.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { card.Id, card.CardStatus, ActivatedAtUtc = DateTime.UtcNow });
    }

    [Authorize(Roles = "Customer")]
    [HttpPost("simulation")]
    public async Task<IActionResult> Simulate(CustomerSimulationRequest request, CancellationToken cancellationToken) =>
        Ok(await platformService.SimulateAsync(new SimulationRequest(
            CustomerId, request.CardTypeId, request.RequestedLimit, "NotApplicable"), cancellationToken));

    // Prototipte kod deterministik üretilir; gerçek ortamda tek kullanımlık kod
    // doğrulanmış SMS/e-posta kanalına gönderilip hash'lenerek saklanmalıdır.
    private static string ActivationCode(int entityId, int relatedId) =>
        ((entityId * 7919L + relatedId * 101L) % 900_000L + 100_000L).ToString();

    private static (string Channel, string Destination) ActivationDestination(CreditCardApplication.Domain.Entities.Customer customer)
    {
        if (customer.IsPhoneVerified && !string.IsNullOrWhiteSpace(customer.PhoneNumber))
            return ("Phone", customer.PhoneCountryCode + customer.PhoneNumber);
        if (customer.IsEmailVerified && !string.IsNullOrWhiteSpace(customer.EmailAddress))
            return ("Email", customer.EmailAddress);
        throw new InvalidOperationException("Aktivasyon için doğrulanmış telefon veya e-posta bilgisi gereklidir.");
    }

    private static string MaskDestination(string destination)
    {
        if (destination.Contains('@'))
        {
            var parts = destination.Split('@', 2);
            return $"{parts[0][0]}***@{parts[1]}";
        }
        return destination.Length <= 4 ? "****" : new string('*', destination.Length - 4) + destination[^4..];
    }

    private int CustomerId => int.TryParse(User.FindFirstValue("customerId"), out var id)
        ? id : throw new UnauthorizedAccessException("Müşteri oturumu geçersiz.");
}
