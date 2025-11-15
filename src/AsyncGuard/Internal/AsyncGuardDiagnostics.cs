using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace AsyncGuard.Internal;

internal static class AsyncGuardDiagnostics
{
    private const string MeterName = "AsyncGuard";

    private static readonly ActivitySource ActivitySource = new("AsyncGuard");
    private static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> Started = Meter.CreateCounter<long>("asyncguard.operations.started");
    private static readonly Counter<long> Completed = Meter.CreateCounter<long>("asyncguard.operations.completed");
    private static readonly Counter<long> Failed = Meter.CreateCounter<long>("asyncguard.operations.failed");
    private static readonly Counter<long> Retried = Meter.CreateCounter<long>("asyncguard.operations.retried");
    private static readonly Counter<long> TimedOut = Meter.CreateCounter<long>("asyncguard.operations.timeout");
    private static readonly Counter<long> Cancelled = Meter.CreateCounter<long>("asyncguard.operations.cancelled");
    private static readonly Histogram<double> Durations = Meter.CreateHistogram<double>("asyncguard.operations.duration", unit: "ms");

    public static ActivitySource ActivitySourceInstance => ActivitySource;

    public static Meter MeterInstance => Meter;

    public static Activity? StartActivity(string taskName, int attempt, ActivityContext parentContext, Activity? parentActivity)
    {
        var activity = ActivitySource.StartActivity(
            "AsyncGuard.FireAndForget",
            ActivityKind.Internal,
            parentContext,
            tags: new[]
            {
                new KeyValuePair<string, object?>("asyncguard.task_name", taskName),
                new KeyValuePair<string, object?>("asyncguard.attempt", attempt)
            });
        if (activity is not null && parentActivity is not null)
        {
            foreach (var baggage in parentActivity.Baggage)
            {
                activity.AddBaggage(baggage.Key, baggage.Value);
            }
        }

        return activity;
    }

    public static TagList CreateTags(string taskName, int attempt)
    {
        var tags = new TagList
        {
            { "asyncguard.task_name", taskName },
            { "asyncguard.attempt", attempt }
        };

        return tags;
    }

    public static void RecordStart(in TagList tags) => Started.Add(1, tags);

    public static void RecordCompletion(in TagList tags) => Completed.Add(1, tags);

    public static void RecordFailure(in TagList tags) => Failed.Add(1, tags);

    public static void RecordRetry(in TagList tags) => Retried.Add(1, tags);

    public static void RecordTimeout(in TagList tags) => TimedOut.Add(1, tags);

    public static void RecordCancellation(in TagList tags) => Cancelled.Add(1, tags);

    public static void RecordDuration(TimeSpan duration, in TagList tags) => Durations.Record(duration.TotalMilliseconds, tags);
}
