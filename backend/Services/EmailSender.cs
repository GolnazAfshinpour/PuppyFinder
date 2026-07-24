using System.Net;
using System.Net.Mail;

namespace PuppyFinder.Api.Services;

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct);
}

/// <summary>
/// Real delivery via SMTP; registered when Smtp:Host is configured
/// (Smtp:Host/Port/User/Password/From in appsettings or environment).
/// </summary>
public sealed class SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct)
    {
        var host = configuration["Smtp:Host"]!;
        var from = configuration["Smtp:From"] ?? configuration["Smtp:User"] ?? "puppyfinder@localhost";

        using var client = new SmtpClient(host, configuration.GetValue("Smtp:Port", 587))
        {
            EnableSsl = configuration.GetValue("Smtp:UseSsl", true),
        };
        if (configuration["Smtp:User"] is { Length: > 0 } user)
        {
            client.Credentials = new NetworkCredential(user, configuration["Smtp:Password"]);
        }

        using var message = new MailMessage(from, to, subject, htmlBody) { IsBodyHtml = true };
        await client.SendMailAsync(message, ct);
        logger.LogInformation("Alert email sent to {To}: {Subject}", to, subject);
    }
}

/// <summary>
/// Fallback when SMTP isn't configured: writes each email as an .html file to
/// data/outbox so the full alert pipeline works (and is inspectable) in dev.
/// </summary>
public sealed class OutboxEmailSender(IHostEnvironment environment, ILogger<OutboxEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct)
    {
        var dir = Path.Combine(environment.ContentRootPath, "data", "outbox");
        Directory.CreateDirectory(dir);
        var safeTo = string.Concat(to.Where(char.IsLetterOrDigit));
        var path = Path.Combine(dir, $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{safeTo}.html");
        await File.WriteAllTextAsync(path, $"<!-- to: {to}\n     subject: {subject} -->\n{htmlBody}", ct);
        logger.LogWarning("SMTP not configured — alert email for {To} written to {Path}", to, path);
    }
}
