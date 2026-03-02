using NuciNotifications.Api.Requests;

namespace NuciNotifications.Api.Service
{
    public interface IEmailService
    {
        void Send(SendEmailRequest request);
    }
}
