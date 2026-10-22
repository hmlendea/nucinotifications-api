using ArchUnitNET.Domain;
using ArchUnitNET.Loader;

using NuciNotifications.API.Service;

namespace NuciNotifications.API.ArchitectureTests
{
    internal static class ProductionArchitecture
    {
        internal static Architecture Model { get; } = new ArchLoader()
            .LoadAssemblies(typeof(IEmailService).Assembly)
            .Build();
    }
}