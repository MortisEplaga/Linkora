using MimeKit;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace Linkora.Services
{
    public interface IEmailService
    {
        Task SendConfirmationEmailAsync(string toEmail, string username, string confirmUrl);
    }

    public class SmtpEmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SmtpEmailService> _logger;

        public SmtpEmailService(IConfiguration configuration, ILogger<SmtpEmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendConfirmationEmailAsync(string toEmail, string username, string confirmUrl)
        {
            var section = _configuration.GetSection("Email");
            var host = section["SmtpHost"] ?? throw new InvalidOperationException("SmtpHost is not configured");
            var port = int.Parse(section["SmtpPort"] ?? throw new InvalidOperationException("SmtpPort is not configured"));
            var user = section["SmtpUser"] ?? throw new InvalidOperationException("SmtpUser is not configured");
            var password = section["SmtpPassword"] ?? throw new InvalidOperationException("SmtpPassword is not configured");
            var fromName = section["FromName"] ?? "noreply";
            var enableSsl = bool.Parse(section["EnableSsl"] ?? "true");

            var body = $@"
<!DOCTYPE html>
<html>
<head>
  <meta charset=""utf-8"" />
  <style>
    body {{ font-family: Arial, sans-serif; background: #f5f5f5; margin: 0; padding: 0; }}
    .wrap {{ max-width: 520px; margin: 40px auto; background: #fff;
             border-radius: 12px; overflow: hidden; border: 1px solid #e8e8e8; }}
    .header {{ background: #1a1a1a; padding: 28px 32px; }}
    .header h1 {{ color: #fff; margin: 0; font-size: 22px; }}
    .body {{ padding: 32px; }}
    .body p {{ color: #333; font-size: 15px; line-height: 1.6; margin: 0 0 16px; }}
    .btn {{ display: inline-block; padding: 13px 28px; background: #00b0a3;
            color: #fff; text-decoration: none; border-radius: 8px;
            font-size: 15px; font-weight: 600; margin: 8px 0 24px; }}
    .note {{ font-size: 13px; color: #aaa; }}
    .link {{ word-break: break-all; font-size: 13px; color: #555; }}
  </style>
</head>
<body>
  <div class=""wrap"">
    <div class=""header""><h1>Linkora</h1></div>
    <div class=""body"">
      <p>Hello, <strong>{username}</strong>!</p>
      <p>Click the button below to confirm your email address and complete registration.</p>
      <a class=""btn"" href=""{confirmUrl}"">Confirm email</a>
      <p class=""note"">If the button does not work, copy this link into your browser:</p>
      <p class=""link"">{confirmUrl}</p>
      <p class=""note"">The link expires in 24 hours. If you did not register on Linkora, ignore this email.</p>
    </div>
  </div>
</body>
</html>";

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(fromName, user));
            message.To.Add(new MailboxAddress(toEmail, toEmail));
            message.Subject = "Confirm your Linkora account";

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = body
            };
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            try
            {
                // Для порта 465 используем SSL сразу (SecureSocketOptions.SslOnConnect)
                // Для портов 587/25 используем STARTTLS (SecureSocketOptions.StartTls) или Auto
                var options = port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
                if (!enableSsl) options = SecureSocketOptions.None;

                await client.ConnectAsync(host, port, options);
                await client.AuthenticateAsync(user, password);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send confirmation email to {Email}", toEmail);
                throw;
            }
        }
    }
}