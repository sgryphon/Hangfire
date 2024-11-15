namespace Hangfire.Diagnostics
{
    public static class DiagnosticHeaders
    {
        public const string DefaultListenerName = "Hangfire";

        public const string TraceParent = "TraceParent";

        public const string TraceState = "TraceState";

        public static class Exceptions
        {
            public const string EventName = "exception";
            public const string Type = "exception.type";
            public const string Message = "exception.message";
            public const string Stacktrace = "exception.stacktrace";
        }

        public static class Messaging
        {
            public const string DestinationName = "messaging.destination.name";
            public const string MessageId = "messaging.message.id";
            public const string OperationName = "messaging.operation.name";
            public const string OperationType = "messaging.operation.type";
        }

        public enum OperationType
        {
            None = 0,
            Create,
            Send,
            Receive,
            Process,
            Settle
        }
    }
}