using Microsoft.AspNetCore.Identity.UI.Services;

namespace Assignment.Services
{
    public class EmailSender : IEmailSender
    {
        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            await MailService.SendMailAsync(email, subject, htmlMessage);
        }
    }
}
