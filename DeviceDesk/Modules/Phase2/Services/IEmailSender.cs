namespace DeviceDesk.Modules.Phase2.Services
{
    public interface IEmailSender
    {
        Task SendEmailAsync(string toEmail, string subject, string body);
    }
}