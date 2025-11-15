using System.Diagnostics.Tracing;

namespace AsyncGuard;

[EventSource(Name = EventSourceName)]
internal sealed class AsyncGuardEventSource : EventSource
{
    public const string EventSourceName = "AsyncGuard";

    public static AsyncGuardEventSource Log { get; } = new AsyncGuardEventSource();

    private AsyncGuardEventSource()
        : base(EventSourceSettings.EtwSelfDescribingEventFormat)
    {
    }

    [Event(1, Level = EventLevel.Informational, Message = "Task {0} started (attempt {1})")]
    public void TaskStarted(string taskName, int attempt)
    {
        if (IsEnabled())
        {
            WriteEvent(1, taskName ?? string.Empty, attempt);
        }
    }

    [Event(2, Level = EventLevel.Informational, Message = "Task {0} completed in {2}ms (attempt {1}/{3})")]
    public void TaskCompleted(string taskName, int attempt, double durationMs, int totalAttempts)
    {
        if (IsEnabled())
        {
            WriteEvent(2, taskName ?? string.Empty, attempt, durationMs, totalAttempts);
        }
    }

    [Event(3, Level = EventLevel.Error, Message = "Task {0} failed on attempt {1}/{3}: {2}")]
    public void TaskFailed(string taskName, int attempt, string exceptionType, int totalAttempts)
    {
        if (IsEnabled())
        {
            WriteEvent(3, taskName ?? string.Empty, attempt, exceptionType ?? string.Empty, totalAttempts);
        }
    }

    [Event(4, Level = EventLevel.Warning, Message = "Task {0} timed out on attempt {1}/{3} after {2}ms")]
    public void TaskTimeout(string taskName, int attempt, double durationMs, int totalAttempts)
    {
        if (IsEnabled())
        {
            WriteEvent(4, taskName ?? string.Empty, attempt, durationMs, totalAttempts);
        }
    }
}
