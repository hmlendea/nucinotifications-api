using NuciLog.Core;

namespace NuciNotifications.Api.Logging
{
    public sealed class MyLogInfoKey : LogInfoKey
    {
        MyLogInfoKey(string name) : base(name) { }

        public static LogInfoKey Attempt => new MyLogInfoKey(nameof(Attempt));

        public static LogInfoKey SenderAddress => new MyLogInfoKey(nameof(SenderAddress));

        public static LogInfoKey SenderName => new MyLogInfoKey(nameof(SenderName));

        public static LogInfoKey Recipient => new MyLogInfoKey(nameof(Recipient));

        public static LogInfoKey Subject => new MyLogInfoKey(nameof(Subject));

        public static LogInfoKey Body => new MyLogInfoKey(nameof(Body));
    }
}
