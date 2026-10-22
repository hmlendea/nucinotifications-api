using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;

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

        [Test]
        public void GivenNoAttachment_WhenSending_ThenTheMessageHasNoAttachments()
        {
            mockSmtpClient.Setup(client => client.Send(It.IsAny<MailMessage>()))
                .Callback<MailMessage>(message => Assert.That(message.Attachments, Is.Empty));

            emailService.Send(BuildSendEmailRequest());

            mockSmtpClient.Verify(client => client.Send(It.IsAny<MailMessage>()), Times.Once);
        }

        [TestCase("U29sYWlyZQ==")]
        [TestCase("AAECA/7/")]
        public void GivenAnAttachment_WhenSending_ThenTheFileNameAndBytesArePreserved(string content)
        {
            SendEmailRequest request = BuildSendEmailRequest();
            request.Attachments = [new() { FileName = "praise-the-sun.bin", Content = content }];
            Stream attachmentStream = null;

            mockSmtpClient.Setup(client => client.Send(It.IsAny<MailMessage>()))
                .Callback<MailMessage>(message =>
                {
                    Assert.That(message.Attachments, Has.Count.EqualTo(1));
                    Attachment attachment = message.Attachments[0];
                    attachmentStream = attachment.ContentStream;
                    using MemoryStream receivedContent = new();
                    attachment.ContentStream.CopyTo(receivedContent);

                    Assert.That(attachment.Name, Is.EqualTo(request.Attachments[0].FileName));
                    Assert.That(receivedContent.ToArray(), Is.EqualTo(Convert.FromBase64String(content)));
                });

            emailService.Send(request);

            mockSmtpClient.Verify(client => client.Send(It.IsAny<MailMessage>()), Times.Once);
            Assert.That(attachmentStream, Is.Not.Null);
            Assert.That(attachmentStream.CanRead, Is.False);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void GivenAnAttachment_WhenSmtpFails_ThenTheStreamIsDisposed(bool doesTimeout)
        {
            SmtpSettings settings = BuildSmtpSettings();
            settings.MaximumAttempts = 0;
            emailService = BuildEmailService(settings);
            SendEmailRequest request = BuildSendEmailRequest();
            request.Attachments = [new() { FileName = "praise-the-sun.txt", Content = "U29sYWlyZQ==" }];
            Stream attachmentStream = null;
            string failureMessage = "SMTP server rejected the message";

            if (doesTimeout)
            {
                failureMessage = "Connection timed out";
            }

            mockSmtpClient.Setup(client => client.Send(It.IsAny<MailMessage>()))
                .Callback<MailMessage>(message => attachmentStream = message.Attachments[0].ContentStream)
                .Throws(new SmtpException(failureMessage));

            Assert.That(() => emailService.Send(request), Throws.Exception);
            Assert.That(attachmentStream, Is.Not.Null);
            Assert.That(attachmentStream.CanRead, Is.False);
            mockSmtpClient.Verify(client => client.Send(It.IsAny<MailMessage>()), Times.Once);
        }

        [Test]
        public void GivenAnAttachment_WhenRetryingAfterATimeout_ThenEachAttemptHasTheCompleteFile()
        {
            SmtpSettings settings = BuildSmtpSettings();
            settings.MaximumAttempts = 1;
            emailService = BuildEmailService(settings);
            SendEmailRequest request = BuildSendEmailRequest();
            request.Attachments = [new() { FileName = "praise-the-sun.txt", Content = "U29sYWlyZQ==" }];
            List<Stream> attachmentStreams = [];

            mockSmtpClient.Setup(client => client.Send(It.IsAny<MailMessage>()))
                .Callback<MailMessage>(message =>
                {
                    Assert.That(message.Attachments, Has.Count.EqualTo(1));
                    Attachment attachment = message.Attachments[0];
                    attachmentStreams.Add(attachment.ContentStream);
                    using StreamReader reader = new(attachment.ContentStream, leaveOpen: true);

                    Assert.That(attachment.Name, Is.EqualTo(request.Attachments[0].FileName));
                    Assert.That(reader.ReadToEnd(), Is.EqualTo("Solaire"));

                    if (attachmentStreams.Count.Equals(1))
                    {
                        throw new SmtpException("Connection timed out");
                    }
                });

            emailService.Send(request);

            mockSmtpClient.Verify(client => client.Send(It.IsAny<MailMessage>()), Times.Exactly(2));
            Assert.That(attachmentStreams[0], Is.Not.SameAs(attachmentStreams[1]));
            Assert.That(attachmentStreams, Has.All.Property(nameof(Stream.CanRead)).False);
        }

        [TestCase("report.pdf", true)]
        [TestCase("REPORT.PDF", true)]
        [TestCase("report.bin", false)]
        public void GivenMultipleAttachments_WhenRetrying_ThenBytesAndMimeTypesArePreserved(
            string fileName,
            bool isPdf)
        {
            byte[] pdfBytes = BuildPdfBytes();
            SendEmailRequest request = BuildSendEmailRequest();
            request.Attachments =
            [
                new() { FileName = fileName, Content = Convert.ToBase64String(pdfBytes) },
                new() { FileName = "greeting.txt", Content = "U29sYWlyZQ==" }
            ];
            List<Stream> streams = [];
            int attempts = 0;
            SmtpSettings settings = BuildSmtpSettings();
            settings.MaximumAttempts = 1;
            emailService = BuildEmailService(settings);
            string expectedMediaType = MediaTypeNames.Application.Octet;

            if (isPdf)
            {
                expectedMediaType = MediaTypeNames.Application.Pdf;
            }

            mockSmtpClient.Setup(client => client.Send(It.IsAny<MailMessage>()))
                .Callback<MailMessage>(message =>
                {
                    attempts += 1;

                    if (attempts > 1)
                    {
                        Assert.That(streams, Has.All.Property(nameof(Stream.CanRead)).False);
                    }

                    Assert.That(message.Attachments, Has.Count.EqualTo(2));
                    Attachment document = message.Attachments[0];
                    streams.Add(document.ContentStream);
                    streams.Add(message.Attachments[1].ContentStream);
                    using MemoryStream received = new();
                    document.ContentStream.CopyTo(received);
                    using StreamReader reader = new(message.Attachments[1].ContentStream, leaveOpen: true);

                    Assert.That(document.Name, Is.EqualTo(fileName));
                    Assert.That(document.ContentType.MediaType, Is.EqualTo(expectedMediaType));
                    Assert.That(document.TransferEncoding, Is.EqualTo(TransferEncoding.Base64));
                    Assert.That(received.ToArray(), Is.EqualTo(pdfBytes));
                    Assert.That(message.Attachments[1].Name, Is.EqualTo("greeting.txt"));
                    Assert.That(reader.ReadToEnd(), Is.EqualTo("Solaire"));

                    if (attempts.Equals(1))
                    {
                        throw new SmtpException("Connection timed out");
                    }
                });

            emailService.Send(request);

            mockSmtpClient.Verify(client => client.Send(It.IsAny<MailMessage>()), Times.Exactly(2));
            Assert.That(streams, Has.All.Property(nameof(Stream.CanRead)).False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void GivenOptionalAttachments_WhenSending_ThenNoFilesAreRequired(bool hasEmptyArray)
        {
            SendEmailRequest request = BuildSendEmailRequest();

            if (hasEmptyArray)
            {
                request.Attachments = [];
            }

            mockSmtpClient.Setup(client => client.Send(It.IsAny<MailMessage>()))
                .Callback<MailMessage>(message => Assert.That(message.Attachments, Is.Empty));

            emailService.Send(request);

            mockSmtpClient.Verify(client => client.Send(It.IsAny<MailMessage>()), Times.Once);
        }

        private static byte[] BuildPdfBytes()
        {
            string[] objects =
            [
                "<< /Type /Catalog /Pages 2 0 R >>",
                "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Resources << >> >>"
            ];
            StringBuilder document = new("%PDF-1.4\n");
            StringBuilder crossReference = new("xref\n0 4\n0000000000 65535 f \n");
            int objectNumber = 1;

            foreach (string pdfObject in objects)
            {
                crossReference.Append(FormattableString.Invariant($"{document.Length:D10} 00000 n \n"));
                document.Append(FormattableString.Invariant($"{objectNumber} 0 obj\n{pdfObject}\nendobj\n"));
                objectNumber += 1;
            }

            int crossReferenceOffset = document.Length;
            document.Append(crossReference);
            document.Append(FormattableString.Invariant(
                $"trailer\n<< /Size 4 /Root 1 0 R >>\nstartxref\n{crossReferenceOffset}\n%%EOF\n"));

            return Encoding.ASCII.GetBytes(document.ToString());
        }

        private EmailService BuildEmailService(
            SmtpSettings smtpSettings)
            => new(
                smtpSettings,
                mockSmtpClient.Object,
                mockLogger.Object);

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
