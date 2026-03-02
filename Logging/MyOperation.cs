using NuciLog.Core;

namespace NuciNotifications.Api.Logging
{
    public sealed class MyOperation : Operation
    {
        MyOperation(string name) : base(name) { }

        public static Operation SendEmail => new MyOperation(nameof(SendEmail));
    }
}
