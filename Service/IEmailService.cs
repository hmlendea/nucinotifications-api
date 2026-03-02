using NuciNotifications.Api.Models;

namespace NuciNotifications.Api.Service
{
    public interface IEmailService
    {
        void Send(SendEmailRequest request);
    }
}
