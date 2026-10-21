using System.Net.Mail;
using System.Text;
using Claim.Application.Claims;
using Microsoft.Extensions.Configuration;

namespace Claim.Infrastructure.Email;

/// <summary>Sends through the SMTP server in the "Smtp" config section (Mailpit on localhost:1025 in development).</summary>
public sealed class SmtpEmailSender(IConfiguration config) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        var host = config["Smtp:Host"] ?? "localhost";
        var port = int.TryParse(config["Smtp:Port"], out var p) ? p : 1025;
        var from = config["Smtp:From"] ?? "noreply@claim.local";

        using var mail = new MailMessage(from, message.ToAddress)
        {
            Subject = message.Subject,
            SubjectEncoding = Encoding.UTF8,
            Body = message.Body,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = false,
        };
        using var client = new SmtpClient(host, port) { Timeout = 10_000 };
        await client.SendMailAsync(mail, ct);
    }
}
