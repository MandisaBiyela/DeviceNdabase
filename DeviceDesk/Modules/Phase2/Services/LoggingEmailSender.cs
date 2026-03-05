using Microsoft.Extensions.Logging;

namespace DeviceDesk.Modules.Phase2.Services
{
    public class LoggingEmailSender : IEmailSender
    {
        private readonly ILogger<LoggingEmailSender> _logger;

        public LoggingEmailSender(ILogger<LoggingEmailSender> logger)
        {
            _logger = logger;
        }

        public Task SendEmailAsync(string toEmail, string subject, string body)
        {
            _logger.LogInformation("[Email] To: {to}\nSubject: {subject}\n{body}", toEmail, subject, body);
            return Task.CompletedTask;
        }
    }
}