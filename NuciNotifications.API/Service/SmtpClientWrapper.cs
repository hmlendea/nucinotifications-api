using System.Net;
using System.Net.Mail;

using NuciNotifications.API.Configuration;

namespace NuciNotifications.API.Service
{
    public sealed class SmtpClientWrapper(SmtpSettings settings) : ISmtpClient
    {
        private readonly SmtpClient smtpClient = BuildSmtpClient(settings);

        private static int SmtpTimeoutInMilliseconds => 200_000;

        public void Send(MailMessage message) => smtpClient.Send(message);

        private static SmtpClient BuildSmtpClient(
            SmtpSettings settings) => new(settings.Host, settings.Port)
        {
            Credentials = new NetworkCredential(settings.Username, settings.Password),
            EnableSsl = true,
            Timeout = SmtpTimeoutInMilliseconds
        };
    }
}
