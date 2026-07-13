using System;
using System.Collections.Generic;
using System.Net.Mail;

using Moq;
using NUnit.Framework;

using NuciLog.Core;

using NuciNotifications.API.Configuration;
using NuciNotifications.API.Requests;
using NuciNotifications.API.Service;

namespace NuciNotifications.API.UnitTests.Service
{
    [TestFixture]
    public class EmailServiceTests
    {
        Mock<ISmtpClient> mockSmtpClient;
        Mock<ILogger> mockLogger;
        EmailService emailService;

        [SetUp]
        public void SetUp()
        {
            mockSmtpClient = new Mock<ISmtpClient>();
            mockLogger = new Mock<ILogger>();
            emailService = BuildEmailService(BuildSmtpSettings());
        }

        // ── Send ──────────────────────────────────────────────────────────────

        [Test]
        public void GivenValidRequest_WhenSendIsCalled_ThenSmtpClientSendIsCalledOnce()
        {
            emailService.Send(BuildSendEmailRequest());

            mockSmtpClient.Verify(
                x => x.Send(It.IsAny<MailMessage>()),
                Times.Once);
        }

        [Test]
        public void GivenValidRequest_WhenSendIsCalled_ThenLogsStarted()
        {
            emailService.Send(BuildSendEmailRequest());

            mockLogger.Verify(
                x => x.Info(
                    It.Is<Operation>(op => op.Name == "SendEmail"),
                    It.Is<OperationStatus>(os => os.Name == "Started"),
                    It.IsAny<IEnumerable<LogInfo>>()),
                Times.Once);
        }

        [Test]
        public void GivenValidRequest_WhenSendIsCalled_ThenLogsSuccess()
        {
            emailService.Send(BuildSendEmailRequest());

            mockLogger.Verify(
                x => x.Info(
                    It.Is<Operation>(op => op.Name == "SendEmail"),
                    It.Is<OperationStatus>(os => os.Name == "Success"),
                    It.IsAny<IEnumerable<LogInfo>>()),
                Times.Once);
        }

        [Test]
        public void GivenValidRequest_WhenSendIsCalled_ThenEmailFromAddressMatchesSettingsUsername()
        {
            emailService.Send(BuildSendEmailRequest());

            mockSmtpClient.Verify(
                x => x.Send(It.Is<MailMessage>(m =>
                    m.From.Address == "notifier@nucilandia.ro")),
                Times.Once);
        }

        [Test]
        public void GivenValidRequest_WhenSendIsCalled_ThenEmailRecipientMatchesRequest()
        {
            SendEmailRequest request = BuildSendEmailRequest();

            emailService.Send(request);

            mockSmtpClient.Verify(
                x => x.Send(It.Is<MailMessage>(m =>
                    m.To.Count == 1 &&
                    m.To[0].Address == request.Recipient)),
                Times.Once);
        }

        [Test]
        public void GivenValidRequest_WhenSendIsCalled_ThenEmailSubjectMatchesRequest()
        {
            SendEmailRequest request = BuildSendEmailRequest();

            emailService.Send(request);

            mockSmtpClient.Verify(
                x => x.Send(It.Is<MailMessage>(m => m.Subject == request.Subject)),
                Times.Once);
        }

        [Test]
        public void GivenValidRequest_WhenSendIsCalled_ThenEmailBodyMatchesRequest()
        {
            SendEmailRequest request = BuildSendEmailRequest();

            emailService.Send(request);

            mockSmtpClient.Verify(
                x => x.Send(It.Is<MailMessage>(m => m.Body == request.Body)),
                Times.Once);
        }

        [Test]
        public void GivenRequestWithNullSender_WhenSendIsCalled_ThenEmailDisplayNameMatchesSettingsSenderName()
        {
            SendEmailRequest request = BuildSendEmailRequest();
            request.Sender = null;

            emailService.Send(request);

            mockSmtpClient.Verify(
                x => x.Send(It.Is<MailMessage>(m =>
                    m.From.DisplayName == "Nucilandia Notifier")),
                Times.Once);
        }

        [Test]
        public void GivenRequestWithEmptySender_WhenSendIsCalled_ThenEmailDisplayNameMatchesSettingsSenderName()
        {
            SendEmailRequest request = BuildSendEmailRequest();
            request.Sender = string.Empty;

            emailService.Send(request);

            mockSmtpClient.Verify(
                x => x.Send(It.Is<MailMessage>(m =>
                    m.From.DisplayName == "Nucilandia Notifier")),
                Times.Once);
        }

        [Test]
        public void GivenRequestWithWhitespaceSender_WhenSendIsCalled_ThenEmailDisplayNameMatchesSettingsSenderName()
        {
            SendEmailRequest request = BuildSendEmailRequest();
            request.Sender = "   ";

            emailService.Send(request);

            mockSmtpClient.Verify(
                x => x.Send(It.Is<MailMessage>(m =>
                    m.From.DisplayName == "Nucilandia Notifier")),
                Times.Once);
        }

        [Test]
        public void GivenRequestWithSender_WhenSendIsCalled_ThenEmailDisplayNameMatchesRequestSender()
        {
            SendEmailRequest request = BuildSendEmailRequest();
            request.Sender = "Solaire of Astora";

            emailService.Send(request);

            mockSmtpClient.Verify(
                x => x.Send(It.Is<MailMessage>(m =>
                    m.From.DisplayName == "Solaire of Astora")),
                Times.Once);
        }

        [Test]
        public void GivenTimedOutSmtpException_WhenMaximumAttemptsNotExceeded_ThenRetriesSending()
        {
            SmtpSettings settings = BuildSmtpSettings();
            settings.MaximumAttempts = 1;

            emailService = BuildEmailService(settings);

            mockSmtpClient
                .SetupSequence(x => x.Send(It.IsAny<MailMessage>()))
                .Throws(new SmtpException("Connection timed out"));

            emailService.Send(BuildSendEmailRequest());

            mockSmtpClient.Verify(
                x => x.Send(It.IsAny<MailMessage>()),
                Times.Exactly(2));
        }

        [Test]
        public void GivenTimedOutSmtpException_WhenMaximumAttemptsExceeded_ThenThrowsTimeoutException()
        {
            SmtpSettings settings = BuildSmtpSettings();
            settings.MaximumAttempts = 0;

            emailService = BuildEmailService(settings);

            mockSmtpClient
                .Setup(x => x.Send(It.IsAny<MailMessage>()))
                .Throws(new SmtpException("Connection timed out"));

            Assert.That(
                () => emailService.Send(BuildSendEmailRequest()),
                Throws.TypeOf<TimeoutException>());
        }

        [Test]
        public void GivenTimedOutSmtpException_WhenMaximumAttemptsExceeded_ThenLogsWarning()
        {
            SmtpSettings settings = BuildSmtpSettings();
            settings.MaximumAttempts = 0;

            emailService = BuildEmailService(settings);

            mockSmtpClient
                .Setup(x => x.Send(It.IsAny<MailMessage>()))
                .Throws(new SmtpException("Connection timed out"));

            Assert.That(
                () => emailService.Send(BuildSendEmailRequest()),
                Throws.TypeOf<TimeoutException>());

            mockLogger.Verify(
                x => x.Warn(
                    It.Is<Operation>(op => op.Name == "SendEmail"),
                    It.Is<OperationStatus>(os => os.Name == "Failure"),
                    It.IsAny<IEnumerable<LogInfo>>(),
                    It.IsAny<LogInfo[]>()),
                Times.Once);
        }

        [Test]
        public void GivenSmtpExceptionWithTimedOutMessage_WhenMaximumAttemptsExceeded_ThenThrowsTimeoutException()
        {
            SmtpSettings settings = BuildSmtpSettings();
            settings.MaximumAttempts = 0;

            emailService = BuildEmailService(settings);

            mockSmtpClient
                .Setup(x => x.Send(It.IsAny<MailMessage>()))
                .Throws(new SmtpException("Connection timed out"));

            Assert.That(
                () => emailService.Send(BuildSendEmailRequest()),
                Throws.TypeOf<TimeoutException>());
        }

        [Test]
        public void GivenSmtpExceptionWithLowercaseTimeoutMessage_WhenMaximumAttemptsExceeded_ThenThrowsTimeoutException()
        {
            SmtpSettings settings = BuildSmtpSettings();
            settings.MaximumAttempts = 0;

            emailService = BuildEmailService(settings);

            mockSmtpClient
                .Setup(x => x.Send(It.IsAny<MailMessage>()))
                .Throws(new SmtpException("SMTP timeout occurred"));

            Assert.That(
                () => emailService.Send(BuildSendEmailRequest()),
                Throws.TypeOf<TimeoutException>());
        }

        [Test]
        public void GivenSmtpExceptionWithCapitalisedTimeoutMessage_WhenMaximumAttemptsExceeded_ThenThrowsTimeoutException()
        {
            SmtpSettings settings = BuildSmtpSettings();
            settings.MaximumAttempts = 0;

            emailService = BuildEmailService(settings);

            mockSmtpClient
                .Setup(x => x.Send(It.IsAny<MailMessage>()))
                .Throws(new SmtpException("Timeout connecting to server"));

            Assert.That(
                () => emailService.Send(BuildSendEmailRequest()),
                Throws.TypeOf<TimeoutException>());
        }

        [Test]
        public void GivenNonTimeoutSmtpException_WhenSendFails_ThenLogsError()
        {
            mockSmtpClient
                .Setup(x => x.Send(It.IsAny<MailMessage>()))
                .Throws(new SmtpException("SMTP server rejected the message"));

            Assert.That(
                () => emailService.Send(BuildSendEmailRequest()),
                Throws.TypeOf<SmtpException>());

            mockLogger.Verify(
                x => x.Error(
                    It.Is<Operation>(op => op.Name == "SendEmail"),
                    It.Is<OperationStatus>(os => os.Name == "Failure"),
                    It.IsAny<Exception>(),
                    It.IsAny<IEnumerable<LogInfo>>()),
                Times.Once);
        }

        [Test]
        public void GivenNonTimeoutSmtpException_WhenSendFails_ThenRethrowsException()
        {
            SmtpException expectedException = new("SMTP server rejected the message");

            mockSmtpClient
                .Setup(x => x.Send(It.IsAny<MailMessage>()))
                .Throws(expectedException);

            Assert.That(
                () => emailService.Send(BuildSendEmailRequest()),
                Throws.Exception.SameAs(expectedException));
        }

        [Test]
        public void GivenGeneralException_WhenSendFails_ThenLogsError()
        {
            mockSmtpClient
                .Setup(x => x.Send(It.IsAny<MailMessage>()))
                .Throws(new InvalidOperationException("Unexpected failure"));

            Assert.That(
                () => emailService.Send(BuildSendEmailRequest()),
                Throws.TypeOf<InvalidOperationException>());

            mockLogger.Verify(
                x => x.Error(
                    It.Is<Operation>(op => op.Name == "SendEmail"),
                    It.Is<OperationStatus>(os => os.Name == "Failure"),
                    It.IsAny<Exception>(),
                    It.IsAny<IEnumerable<LogInfo>>()),
                Times.Once);
        }

        [Test]
        public void GivenGeneralException_WhenSendFails_ThenRethrowsException()
        {
            InvalidOperationException expectedException = new("Unexpected failure");

            mockSmtpClient
                .Setup(x => x.Send(It.IsAny<MailMessage>()))
                .Throws(expectedException);

            Assert.That(
                () => emailService.Send(BuildSendEmailRequest()),
                Throws.Exception.SameAs(expectedException));
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private EmailService BuildEmailService(SmtpSettings smtpSettings)
            => new(smtpSettings, mockSmtpClient.Object, mockLogger.Object);

        private static SmtpSettings BuildSmtpSettings()
            => new()
            {
                Host = "mail.nucilandia.ro",
                Port = 587,
                Username = "notifier@nucilandia.ro",
                Password = "testpassword",
                SenderName = "Nucilandia Notifier",
                MaximumAttempts = 3,
                DelayBetweenAttemptsInSeconds = 0
            };

        private static SendEmailRequest BuildSendEmailRequest()
            => new()
            {
                Sender = "Solaire of Astora",
                Recipient = "vasile.ciupitu@gmail.com",
                Subject = "Praise the Sun!",
                Body = "Would you like to join me on a jolly co-op adventure?"
            };
    }
}
