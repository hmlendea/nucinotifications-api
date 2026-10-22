using System.Linq;

using ArchUnitNET.Domain;
using ArchUnitNET.NUnit;

using NUnit.Framework;

using NuciNotifications.API.Service;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace NuciNotifications.API.ArchitectureTests
{
    [TestFixture]
    public sealed class DependencyTests
    {
        [TestCase("Service", "Controllers")]
        [TestCase("Requests", "Controllers")]
        [TestCase("Requests", "Service")]
        [TestCase("Requests", "Configuration")]
        [TestCase("Requests", "Logging")]
        [TestCase("Configuration", "Controllers")]
        [TestCase("Configuration", "Service")]
        [TestCase("Configuration", "Requests")]
        [TestCase("Configuration", "Logging")]
        [TestCase("Logging", "Controllers")]
        [TestCase("Logging", "Service")]
        [TestCase("Logging", "Requests")]
        [TestCase("Logging", "Configuration")]
        public void GivenComponents_WhenCheckingDependencies_ThenBoundariesAreRespected(
            string sourceComponent,
            string forbiddenComponent)
        {
            IObjectProvider<IType> sourceTypes = Types().That()
                .ResideInNamespace($"NuciNotifications.API.{sourceComponent}");
            IObjectProvider<IType> forbiddenTypes = Types().That()
                .ResideInNamespace($"NuciNotifications.API.{forbiddenComponent}");

            Assert.That(
                sourceTypes.GetObjects(ProductionArchitecture.Model).ToArray(),
                Is.Not.Empty,
                $"The {sourceComponent} component must contain production types.");
            Assert.That(
                forbiddenTypes.GetObjects(ProductionArchitecture.Model).ToArray(),
                Is.Not.Empty,
                $"The {forbiddenComponent} component must contain production types.");

            Types().That().Are(sourceTypes).Should().NotDependOnAny(forbiddenTypes)
                .Because("The documented controller-service-adapter flow must remain acyclic.")
                .Check(ProductionArchitecture.Model);
        }

        [TestCase(typeof(EmailService))]
        [TestCase(typeof(SmtpClientWrapper))]
        [TestCase(typeof(ISmtpClient))]
        public void GivenControllers_WhenCheckingDependencies_ThenDeliveryImplementationsAreIsolated(
            System.Type forbiddenType)
        {
            IObjectProvider<IType> controllers = Types().That()
                .ResideInNamespace("NuciNotifications.API.Controllers");

            Assert.That(
                controllers.GetObjects(ProductionArchitecture.Model).ToArray(),
                Is.Not.Empty,
                "The HTTP boundary must contain production controllers.");

            Types().That().Are(controllers).Should().NotDependOnAny(forbiddenType)
                .Because("Controllers delegate delivery through IEmailService, not SMTP implementations.")
                .Check(ProductionArchitecture.Model);
        }

        [Test]
        public void GivenEmailService_WhenCheckingDependencies_ThenTheSmtpAdapterRemainsBehindItsPort()
            => Types().That().Are(typeof(EmailService)).Should()
                .NotDependOnAny(typeof(SmtpClientWrapper))
                .Because("EmailService must depend on ISmtpClient rather than its concrete adapter.")
                .Check(ProductionArchitecture.Model);
    }
}