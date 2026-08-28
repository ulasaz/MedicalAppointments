using MailKit.Net.Smtp;
using MimeKit;
using Notifications.Interfaces;

namespace Notifications.Services;

public class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _config;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IConfiguration config, ILogger<SmtpEmailSender> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string toName, string subject, string body)
    {
        var host = _config["Smtp:Host"] ?? "localhost";
        var port = _config.GetValue("Smtp:Port", 1025);
        var fromEmail = _config["Smtp:FromEmail"] ?? "no-reply@curaslot.local";
        var fromName = _config["Smtp:FromName"] ?? "CuraSlot";

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromEmail));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        try
        {
            using var client = new SmtpClient();
            await client.ConnectAsync(host, port, MailKit.Security.SecureSocketOptions.None);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            // A notification failing to send should never take down the consumer or cause the
            // triggering appointment action to be retried/redelivered — log and move on.
            _logger.LogError(ex, "Failed to send email to {ToEmail}", toEmail);
        }
    }
}
