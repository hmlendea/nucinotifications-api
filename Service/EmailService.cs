using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Mail;
using System.Threading;
using NuciLog.Core;
using NuciNotifications.Api.Requests;
using NuciNotifications.Api.Configuration;
using NuciNotifications.Api.Logging;

namespace NuciNotifications.Api.Service
{
    public class EmailService(
        SmtpSettings settings,
        ILogger logger) : IEmailService
    {
        private readonly SmtpClient smtpClient = new(settings.Host, settings.Port)
        {
            Credentials = new NetworkCredential(settings.Username, settings.Password),
            EnableSsl = true,
            Timeout = 200000
        };

        public void Send(SendEmailRequest request)
            => Send(request, settings.MaximumAttempts);

        public void Send(SendEmailRequest request, int attemptsLeft)
        {
            string sender = string.IsNullOrEmpty(request.Sender) ?
                settings.Username :
                request.Sender;

            IEnumerable<LogInfo> logInfos =
            [
                new(MyLogInfoKey.Sender, sender),
                new(MyLogInfoKey.Recipient, request.Recipient),
                new(MyLogInfoKey.Subject, request.Subject),
                new(MyLogInfoKey.Body, request.Body)
            ];

            logger.Info(MyOperation.SendEmail, OperationStatus.Started, logInfos);

            using MailMessage message = new(
                sender,
                request.Recipient,
                request.Subject,
                request.Body);

            try
            {
                smtpClient.Send(message);
            }
            catch (SmtpException ex) when (ex.Message.Contains("timed out"))
            {
                logger.Warn(
                    MyOperation.SendEmail,
                    OperationStatus.Failure,
                    logInfos,
                    new LogInfo(MyLogInfoKey.Attempt, settings.MaximumAttempts - attemptsLeft + 1));

                if (attemptsLeft <= 0)
                {
                    throw;
                }

                Thread.Sleep(settings.DelayBetweenAttemptsInSeconds * 1000);

                Send(request, attemptsLeft - 1);
            }
            catch (Exception ex)
            {
                logger.Error(
                    MyOperation.SendEmail,
                    OperationStatus.Failure,
                    ex,
                    logInfos);

                throw;
            }
        }
    }
}