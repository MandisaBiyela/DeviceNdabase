using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DeviceDesk.Modules.Phase2.Services
{
    public class SmtpEmailSender : IEmailSender
    {
        private readonly ILogger<SmtpEmailSender> _logger;
        private readonly string _host;
        private readonly int _port;
        private readonly bool _useSsl;
        private readonly string _user;
        private readonly string _password;
        private readonly string _from;

        public SmtpEmailSender(IConfigurationSection section, ILogger<SmtpEmailSender> logger)
        {
            _logger = logger;

            _host = section["Host"] ?? "";
            _port = int.TryParse(section["Port"], out var p) ? p : 25;
            _useSsl = bool.TryParse(section["UseSsl"], out var ssl) ? ssl : true;
            _user = section["User"] ?? "";
            _password = section["Password"] ?? "";
            _from = section["From"] ?? _user;
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            _logger.LogInformation(
                "SMTP: sending email to {To} via {Host}:{Port} as {User}",
                to, _host, _port, _user
            );

            using var message = new MailMessage(_from, to, subject, body)
            {
                IsBodyHtml = false
            };

            using var client = new SmtpClient(_host, _port)
            {
                EnableSsl = _useSsl,
                UseDefaultCredentials = false,
                Credentials = string.IsNullOrWhiteSpace(_user)
                    ? null
                    : new NetworkCredential(_user, _password),
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            try
            {
                await client.SendMailAsync(message);
                _logger.LogInformation("SMTP: email to {To} sent successfully.", to);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SMTP: failed to send email to {To}", to);
                throw; // bubble up so you see real errors while debugging
            }
        }
    }
}