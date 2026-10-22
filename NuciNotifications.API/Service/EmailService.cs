using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mail;
using System.Net.Mime;
using System.Threading;

using NuciLog.Core;

using NuciNotifications.API.Configuration;
using NuciNotifications.API.Logging;
using NuciNotifications.API.Requests;

namespace NuciNotifications.API.Service
{
    public class EmailService(
        SmtpSettings settings,
        ISmtpClient smtpClient,
        ILogger logger) : IEmailService
    {
        private static int MillisecondsPerSecond => 1_000;

        public void Send(SendEmailRequest request)
            => Send(request, settings.MaximumAttempts);

        private void Send(
            SendEmailRequest request,
            int attemptsLeft)
        {
            string senderName = settings.SenderName;

            if (!string.IsNullOrWhiteSpace(request.Sender))
            {
                senderName = request.Sender;
            }

            IEnumerable<LogInfo> logInfos =
            [
                new(MyLogInfoKey.SenderAddress, settings.Username),
                new(MyLogInfoKey.SenderName, senderName),
                new(MyLogInfoKey.Recipient, request.Recipient),
                new(MyLogInfoKey.Subject, request.Subject)
            ];

            logger.Info(
                MyOperation.SendEmail,
                OperationStatus.Started,
                logInfos);

            using MailMessage message = new(
                settings.Username,
                request.Recipient,
                request.Subject,
                request.Body);

            message.From = new(settings.Username, senderName);

            try
            {
                if (request.Attachments is not null)
                {
                    foreach (EmailAttachmentRequest attachment in request.Attachments)
                    {
                        AddAttachment(message, attachment.FileName, attachment.Content);
                    }
                }

                smtpClient.Send(message);

                logger.Info(
                    MyOperation.SendEmail,
                    OperationStatus.Success,
                    logInfos);
            }
            catch (SmtpException exception) when (
                exception.Message.Contains("timed out") ||
                exception.Message.Contains("timeout") ||
                exception.Message.Contains("Timeout"))
            {
                logger.Warn(
                    MyOperation.SendEmail,
                    OperationStatus.Failure,
                    logInfos,
                    new LogInfo(
                        MyLogInfoKey.Attempt,
                        settings.MaximumAttempts - attemptsLeft + 1));

                if (attemptsLeft <= 0)
                {
                    throw new TimeoutException(
                        "Failed to send the e-mail notification after the maximum number of attempts.",
                        exception);
                }

                message.Dispose();

                Thread.Sleep(
                    settings.DelayBetweenAttemptsInSeconds * MillisecondsPerSecond);
                Send(request, attemptsLeft - 1);
            }
            catch (Exception exception)
            {
                logger.Error(
                    MyOperation.SendEmail,
                    OperationStatus.Failure,
                    exception,
                    logInfos);

                throw;
            }
        }

        private static void AddAttachment(MailMessage message, string fileName, string content)
        {
            string mediaType = MediaTypeNames.Application.Octet;

            if (string.Equals(Path.GetExtension(fileName), ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                mediaType = MediaTypeNames.Application.Pdf;
            }

            Attachment attachment = new(
                new MemoryStream(Convert.FromBase64String(content)),
                fileName,
                mediaType);
            attachment.TransferEncoding = TransferEncoding.Base64;
            message.Attachments.Add(attachment);
        }
    }
}