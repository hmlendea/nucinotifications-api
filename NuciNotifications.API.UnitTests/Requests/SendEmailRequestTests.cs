using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

using NUnit.Framework;

using NuciNotifications.API.Requests;

namespace NuciNotifications.API.UnitTests.Requests
{
    [TestFixture]
    public sealed class SendEmailRequestTests
    {
        [TestCase(null, null, false)]
        [TestCase("praise-the-sun.txt", "U29sYWlyZQ==", true)]
        [TestCase("praise-the-sun.bin", "AAECA/7/", true)]
        [TestCase(null, "U29sYWlyZQ==", false)]
        [TestCase("praise-the-sun.txt", null, false)]
        [TestCase("", "U29sYWlyZQ==", false)]
        [TestCase("praise-the-sun.txt", "", false)]
        [TestCase("praise-the-sun.txt", "not base64!", false)]
        [TestCase("../praise-the-sun.txt", "U29sYWlyZQ==", false)]
        [TestCase(@"..\praise-the-sun.txt", "U29sYWlyZQ==", false)]
        [TestCase("praise\r\nsun.txt", "U29sYWlyZQ==", false)]
        [TestCase("praise-the-sun.txt\n", "U29sYWlyZQ==", false)]
        public void GivenAttachmentFields_WhenValidating_ThenValidityMatchesTheContract(
            string fileName,
            string content,
            bool isExpectedToBeValid)
        {
            SendEmailRequest request = new()
            {
                Recipient = "vasile.ciupitu@gmail.com",
                Subject = "Praise the Sun!",
                Body = "Solaire",
                Attachments = [new() { FileName = fileName, Content = content }]
            };
            List<ValidationResult> validationResults = [];

            bool isValid = Validator.TryValidateObject(
                request,
                new ValidationContext(request),
                validationResults,
                true);

            Assert.That(isValid, Is.EqualTo(isExpectedToBeValid));
        }

        [TestCase(255, true)]
        [TestCase(256, false)]
        public void GivenAnAttachmentFileName_WhenValidating_ThenTheLengthLimitIsEnforced(
            int fileNameLength,
            bool isExpectedToBeValid)
        {
            SendEmailRequest request = new()
            {
                Recipient = "vasile.ciupitu@gmail.com",
                Subject = "Praise the Sun!",
                Body = "Solaire",
                Attachments =
                [
                    new() { FileName = new string('a', fileNameLength), Content = "U29sYWlyZQ==" }
                ]
            };

            bool isValid = Validator.TryValidateObject(
                request,
                new ValidationContext(request),
                [],
                true);

            Assert.That(isValid, Is.EqualTo(isExpectedToBeValid));
        }

        [TestCase("{}", true)]
        [TestCase("{\"attachments\":null}", true)]
        [TestCase("{\"attachments\":[]}", true)]
        [TestCase("{\"attachments\":[null]}", false)]
        [TestCase("{\"attachments\":[{}]}", false)]
        [TestCase("{\"attachments\":[{\"fileName\":\"report.pdf\",\"content\":\"JVBERi0xLjQ=\"}]}", true)]
        [TestCase("{\"attachments\":[{\"fileName\":\"report.pdf\",\"content\":\"invalid!\"}]}", false)]
        [TestCase("{\"attachments\":[{\"fileName\":\"../report.pdf\",\"content\":\"JVBERi0xLjQ=\"}]}", false)]
        public void GivenAnAttachmentArray_WhenValidating_ThenEveryEntryMustBeValid(
            string json,
            bool isExpectedToBeValid)
        {
            SendEmailRequest request = JsonSerializer.Deserialize<SendEmailRequest>(json, JsonSerializerOptions.Web);
            request.Recipient = "vasile.ciupitu@gmail.com";
            request.Subject = "Praise the Sun!";
            request.Body = "Solaire";

            bool isValid = Validator.TryValidateObject(request, new ValidationContext(request), [], true);

            Assert.That(isValid, Is.EqualTo(isExpectedToBeValid));
        }

        [Test]
        public void GivenOrderedAttachments_WhenSigning_ThenTheExactFileNamesAndContentsAreIncluded()
        {
            SendEmailRequest request = new()
            {
                Attachments =
                [
                    new() { FileName = "report.pdf", Content = "JVBERi0xLjQ=" },
                    new() { FileName = "greeting.txt", Content = "U29sYWlyZQ==" }
                ]
            };

            Assert.That(request.AttachmentsSignatureContent, Is.EqualTo(
                "[{\"fileName\":\"report.pdf\",\"content\":\"JVBERi0xLjQ=\"},{\"fileName\":\"greeting.txt\",\"content\":\"U29sYWlyZQ==\"}]"));
        }

        [Test]
        public void GivenAJsonAttachment_WhenDeserializing_ThenTheFileFieldsAreBound()
        {
            string json = """
                {"attachments":[{"fileName":"praise-the-sun.txt","content":"U29sYWlyZQ=="}]}
                """;
            SendEmailRequest request = JsonSerializer.Deserialize<SendEmailRequest>(
                json,
                JsonSerializerOptions.Web);

            Assert.That(request.Attachments, Has.Length.EqualTo(1));
            Assert.That(request.Attachments[0].FileName, Is.EqualTo("praise-the-sun.txt"));
            Assert.That(request.Attachments[0].Content, Is.EqualTo("U29sYWlyZQ=="));
        }
    }
}