using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NuciLog;
using NuciLog.Configuration;
using NuciLog.Core;
using NuciNotifications.Api.Configuration;
using NuciNotifications.Api.Service;

namespace NuciNotifications.Api
{
    public static class ServiceCollectionExtensions
    {
        static SecuritySettings securitySettings;
        static SmtpSettings smtpSettings;
        static NuciLoggerSettings loggingSettings;

        public static IServiceCollection AddConfigurations(this IServiceCollection services, IConfiguration configuration)
        {
            securitySettings = new SecuritySettings();
            smtpSettings = new SmtpSettings();
            loggingSettings = new NuciLoggerSettings();

            configuration.Bind(nameof(SecuritySettings), securitySettings);
            configuration.Bind(nameof(SmtpSettings), smtpSettings);
            configuration.Bind(nameof(NuciLoggerSettings), loggingSettings);

            services.AddSingleton(securitySettings);
            services.AddSingleton(smtpSettings);
            services.AddSingleton(loggingSettings);

            return services;
        }

        public static IServiceCollection AddCustomServices(this IServiceCollection services) => services
            .AddSingleton<ILogger, NuciLogger>()
            .AddSingleton<IEmailService, EmailService>();
    }
}
