using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NuciNotifications.Api.Configuration;
using NuciNotifications.Api.Service;

namespace NuciNotifications.Api
{
    public static class ServiceCollectionExtensions
    {
        static SmtpSettings smtpSettings;

        public static IServiceCollection AddConfigurations(this IServiceCollection services, IConfiguration configuration)
        {
            smtpSettings = new SmtpSettings();

            configuration.Bind(nameof(SmtpSettings), smtpSettings);

            services.AddSingleton(smtpSettings);

            return services;
        }

        public static IServiceCollection AddCustomServices(this IServiceCollection services) => services
            .AddSingleton<IEmailService, EmailService>();
    }
}
