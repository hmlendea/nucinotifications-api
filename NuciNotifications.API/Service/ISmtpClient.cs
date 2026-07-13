using System.Net.Mail;

namespace NuciNotifications.API.Service
{
    public interface ISmtpClient
    {
        void Send(MailMessage message);
    }
}
