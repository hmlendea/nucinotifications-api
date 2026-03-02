namespace NuciNotifications.Api.Configuration
{
    public sealed class SmtpSettings
    {
        public string Host { get; set; }

        public int Port { get; set; } = 587;

        public string Username { get; set; }

        public string Password { get; set; }

        public int MaximumAttempts { get; set; } = 3;

        public int DelayBetweenAttemptsInSeconds { get; set; } = 5;
    }
}
