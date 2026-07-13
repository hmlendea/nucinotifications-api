using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NuciLog;
using NuciLog.Core;

using NuciNotifications.API.Configuration;
using NuciNotifications.API.Service;

namespace NuciNotifications.API
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddConfigurations(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            SecuritySettings securitySettings = new();
            SmtpSettings smtpSettings = new();

            configuration.Bind(
                nameof(SecuritySettings),
                securitySettings);
            configuration.Bind(nameof(SmtpSettings), smtpSettings);

            return services
                .AddSingleton(securitySettings)
                .AddSingleton(smtpSettings)
                .AddNuciLoggerSettings(configuration);
        }

        public static IServiceCollection AddCustomServices(this IServiceCollection services) => services
            .AddSingleton<ILogger, NuciLogger>()
            .AddSingleton<ISmtpClient, SmtpClientWrapper>()
            .AddSingleton<IEmailService, EmailService>();
    }
}
