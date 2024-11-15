#if NETSTANDARD2_0
#nullable enable

using System;
using System.Diagnostics;
using Hangfire.Client;
using Hangfire.Server;

namespace Hangfire.Diagnostics
{
    internal static class DiagnosticActivityExtensions
    {
        private static readonly ActivitySource HangfireActivitySource = new ActivitySource(DiagnosticHeaders.DefaultListenerName);

        public static void AddExceptionEvent(this Activity activity, Exception exception)
        {
            var tags = new ActivityTagsCollection
            {
                { DiagnosticHeaders.Exceptions.Message, exception.Message },
                { DiagnosticHeaders.Exceptions.Type, exception.GetType() },
                { DiagnosticHeaders.Exceptions.Stacktrace, exception.ToString() }
            };

            var activityEvent = new ActivityEvent(DiagnosticHeaders.Exceptions.EventName, tags: tags);
            activity.AddEvent(activityEvent);

            // NOTE: Need library 6.0 for Status (use tags instead)
            activity.SetTag("otel.status_code", "ERROR");
            activity.SetTag("otel.status_description", exception.Message);
            //activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        }

        public static void AddMessageId(this Activity activity, string messageId)
        {
            activity.AddTag(DiagnosticHeaders.Messaging.MessageId, messageId);
        }

        public static void PropagateCreationContext(this CreateContext toCreateContext, RecurringJobEntity fromRecurringJob)
        {
            toCreateContext.Parameters[DiagnosticHeaders.TraceParent] = fromRecurringJob.TraceParent;
            toCreateContext.Parameters[DiagnosticHeaders.TraceState] = fromRecurringJob.TraceState;
        }

        public static Activity? StartActivity(this CreateContext context)
        {
            var activity = InternalStartActivity(
                "create_background_job",
                context.Job.Queue,
                ActivityKind.Producer,
                DiagnosticHeaders.OperationType.Create
            );
            if (activity != null)
            {
                context.Parameters[DiagnosticHeaders.TraceParent] = activity.Id;
                context.Parameters[DiagnosticHeaders.TraceState] = activity.TraceStateString;
            }
            return activity;
        }

        public static Activity? StartActivity(this PerformContext performContext)
        {
            var traceParent = performContext.GetJobParameter<string>(DiagnosticHeaders.TraceParent);                  
            var traceState = performContext.GetJobParameter<string>(DiagnosticHeaders.TraceState);
            var parentContext = !string.IsNullOrWhiteSpace(traceParent)
                && ActivityContext.TryParse(traceParent, traceState, out var activityContext)
                    ? activityContext
                    : (ActivityContext?)null;
            
            var recurringJobId = performContext.GetJobParameter<string>("RecurringJobId");
            var messageId = !string.IsNullOrWhiteSpace(recurringJobId) ? $"recurring:{recurringJobId}" : performContext.BackgroundJob.Id;

            var activity = InternalStartActivity(
                "perform_job",
                performContext.BackgroundJob.Job.Queue,
                ActivityKind.Consumer,
                DiagnosticHeaders.OperationType.Process,
                messageId,
                parentContext
            );

            return activity;
        }

        public static Activity? StartActivity(this RecurringJobEntity recurringJob)
        {
            var activity = InternalStartActivity(
                "create_recurring",
                recurringJob.Queue,
                ActivityKind.Producer,
                DiagnosticHeaders.OperationType.Create,
                $"recurring:{recurringJob.RecurringJobId}"
            );

            if (activity != null)
            {
                recurringJob.TraceParent = activity.Id;
                recurringJob.TraceState = activity.TraceStateString;
            }
            return activity;
        }

        private static Activity? InternalStartActivity(
            string operationName, 
            string destination, 
            ActivityKind activityKind,
            DiagnosticHeaders.OperationType operationType,
            string? messageId = default,
            ActivityContext? parentContext = default)
        {
            // See: https://opentelemetry.io/docs/concepts/signals/traces/#producer
            // See: https://opentelemetry.io/docs/specs/semconv/messaging/messaging-spans/#span-name
            var spanName = !string.IsNullOrWhiteSpace(destination) 
                    ? $"{operationName} {destination}" 
                    : operationName;
            var activity = parentContext.HasValue
                ? HangfireActivitySource.StartActivity(spanName, activityKind, parentContext.Value)
                : HangfireActivitySource.StartActivity(spanName, activityKind);
            if (activity != null)
            {
                activity.AddTag(DiagnosticHeaders.Messaging.OperationName, operationName);
                if (!string.IsNullOrWhiteSpace(destination))
                {
                    activity.AddTag(DiagnosticHeaders.Messaging.DestinationName, destination);
                }
                activity.AddTag(DiagnosticHeaders.Messaging.OperationType, operationType.ToString().ToLowerInvariant());
                if (!string.IsNullOrWhiteSpace(messageId))
                {
                    activity.AddTag(DiagnosticHeaders.Messaging.MessageId, messageId);
                }
            }
            return activity;
        }
    }
}
#endif