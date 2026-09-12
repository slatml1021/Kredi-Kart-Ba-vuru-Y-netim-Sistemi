using System.Net;
using System.Net.Mail;
using CreditCardApplication.Application.Customers;
using Microsoft.Extensions.Configuration;

namespace CreditCardApplication.Infrastructure.Platform;

public sealed class ContactVerificationSender(IConfiguration configuration) : IContactVerificationSender
{
    public bool ExposeDemoCode =>
        !string.Equals(configuration["ContactVerification:ExposeDemoCode"], "false", StringComparison.OrdinalIgnoreCase);

    public async Task SendAsync(
        string channel, string destination, string code, CancellationToken cancellationToken)
    {
        if (channel == "Email" && string.Equals(configuration["Email:Mode"], "Smtp", StringComparison.OrdinalIgnoreCase))
        {
            var host = configuration["Email:Smtp:Host"] ?? throw new InvalidOperationException("SMTP sunucusu yapılandırılmamış.");
            using var message = new MailMessage(configuration["Email:From"] ?? "no-reply@kartbasvuru.local", destination,
                "İletişim doğrulama kodunuz", $"Doğrulama kodunuz: {code}\nKod 5 dakika geçerlidir.");
            using var client = new SmtpClient(host, int.TryParse(configuration["Email:Smtp:Port"], out var port) ? port : 587)
            {
                EnableSsl = !string.Equals(configuration["Email:Smtp:EnableSsl"], "false", StringComparison.OrdinalIgnoreCase),
                Credentials = new NetworkCredential(configuration["Email:Smtp:Username"], configuration["Email:Smtp:Password"])
            };
            await client.SendMailAsync(message, cancellationToken);
            return;
        }

        var outbox = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", channel == "Phone" ? "sms-outbox" : "email-outbox");
        Directory.CreateDirectory(outbox);
        var safeDestination = new string(destination.Where(char.IsLetterOrDigit).ToArray());
        await File.WriteAllTextAsync(Path.Combine(outbox, $"{DateTime.UtcNow:yyyyMMddHHmmss}-{safeDestination}.txt"),
            $"DESTINATION: {destination}\nCODE: {code}\nEXPIRES: {DateTime.UtcNow.AddMinutes(5):O}", cancellationToken);
    }
}
