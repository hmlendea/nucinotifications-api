using NuciNotifications.API.Requests;

namespace NuciNotifications.API.Service
{
    public interface IEmailService
    {
        void Send(SendEmailRequest request);
    }
}
