using System;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NuciNotifications.Api.Configuration;

namespace NuciNotifications.Api
{
    public class Startup(IConfiguration configuration)
    {
        public IConfiguration Configuration => configuration;

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();

            services
                .AddConfigurations(Configuration)
                .AddCustomServices();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            // Ensure the log stores exist
            var dataStoreSettings = app.ApplicationServices.GetRequiredService<DataStoreSettings>();
            CreateStoreIfMissing(dataStoreSettings.LogStorePath);

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();
            app.UseDefaultFiles();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

        static void CreateStoreIfMissing(string storePath)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(storePath);

            var storeDirectory = Path.GetDirectoryName(storePath);

            if (!Directory.Exists(storeDirectory))
            {
                Directory.CreateDirectory(storeDirectory);
            }

            if (!File.Exists(storePath))
            {
                File.WriteAllText(storePath, "[]");
            }
        }
    }
}
