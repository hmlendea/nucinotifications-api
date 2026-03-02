using System.Net;
using System.Net.Mail;
using System.Threading;
using NuciNotifications.Api.Models;
using NuciNotifications.Api.Configuration;

namespace NuciNotifications.Api.Service
{
    public class EmailService(SmtpSettings settings) : IEmailService
    {
        private readonly SmtpClient smtpClient = new(settings.Host, settings.Port)
        {
            Credentials = new NetworkCredential(settings.Username, settings.Password),
            EnableSsl = true,
            Timeout = 200000
        };

        public void Send(SendEmailRequest request)
            => Send(request, settings.MaximumAttempts);

        public void Send(SendEmailRequest request, int attemptsLeft)
        {
            try
            {
                using MailMessage message = new(
                    settings.Username,
                    request.Recipient,
                    request.Subject,
                    request.Body);

                smtpClient.Send(message);
            }
            catch (SmtpException ex) when (ex.Message.Contains("timed out"))
            {
                if (attemptsLeft <= 0)
                {
                    throw;
                }

                Thread.Sleep(settings.DelayBetweenAttemptsInSeconds * 1000);

                Send(request, attemptsLeft - 1);
            }
        }
    }
}