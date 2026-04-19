using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NuciLog;
using NuciLog.Core;

using NuciNotifications.Api.Configuration;
using NuciNotifications.Api.Service;

namespace NuciNotifications.Api
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddConfigurations(this IServiceCollection services, IConfiguration configuration)
        {
            SecuritySettings securitySettings = new();
            SmtpSettings smtpSettings = new();

            configuration.Bind(nameof(SecuritySettings), securitySettings);
            configuration.Bind(nameof(SmtpSettings), smtpSettings);

            return services
                .AddSingleton(securitySettings)
                .AddSingleton(smtpSettings)
                .AddNuciLoggerSettings(configuration);
        }

        public static IServiceCollection AddCustomServices(this IServiceCollection services) => services
            .AddSingleton<ILogger, NuciLogger>()
            .AddSingleton<IEmailService, EmailService>();
    }
}
