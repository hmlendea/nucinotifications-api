using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

using NUnit.Framework;

using NuciNotifications.API.Service;

namespace NuciNotifications.API.ArchitectureTests
{
    [TestFixture]
    public sealed class ProductionProjectTests
    {
        private static readonly Assembly productionAssembly = typeof(IEmailService).Assembly;

        private static readonly XDocument productionProject = XDocument.Load(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "ProductionProjects",
            "NuciNotifications.API.csproj"));

        [Test]
        public void GivenTheProductionAssembly_WhenCheckingVisibility_ThenInternalsAreNotExposed()
            => Assert.That(
                productionAssembly.GetCustomAttributes<InternalsVisibleToAttribute>(),
                Is.Empty,
                "Production internals must not be exposed to other assemblies for testing.");

        [TestCase("InternalsVisibleTo")]
        [TestCase("ProjectReference")]
        [TestCase("Reference")]
        public void GivenTheProductionProject_WhenCheckingReferences_ThenItRemainsAStandaloneService(
            string elementName)
            => Assert.That(
                productionProject.Descendants().Where(element =>
                    string.Equals(element.Name.LocalName, elementName, StringComparison.Ordinal)),
                Is.Empty,
                "The single production project must not expose internals or add project or raw assembly dependencies.");

        [Test]
        public void GivenTheProductionAssembly_WhenCheckingReferences_ThenTestsAndPersistenceAreExcluded()
            => Assert.That(
                productionAssembly.GetReferencedAssemblies().Select(reference => reference.Name)
                    .Where(name => name is not null && IsForbiddenReference(name)),
                Is.Empty,
                "The database-free HTTP service must not depend on test assemblies or persistence frameworks.");

        [Test]
        public void GivenTheProductionProject_WhenCheckingPackages_ThenTestsAndPersistenceAreExcluded()
            => Assert.That(
                productionProject.Descendants("PackageReference")
                    .Select(element => (string?)element.Attribute("Include"))
                    .Where(name => name is not null && IsForbiddenReference(name)),
                Is.Empty,
                "Declared dependencies must preserve the database-free production boundary.");

        private static bool IsForbiddenReference(string name)
            => name.Contains("Test", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("NUnit", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("xunit", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Moq", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("ArchUnitNET", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("TngTech.ArchUnitNET", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("AWSSDK.DynamoDB", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Dapper", StringComparison.OrdinalIgnoreCase);
    }
}