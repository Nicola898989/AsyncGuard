using System;
using System.Diagnostics.Tracing;
using System.Linq;
using AsyncGuard;
using AsyncGuard.Tests.Infrastructure;

namespace AsyncGuard.Tests;

public class EventSourceTests
{
    [Fact]
    public async Task EmitsStartAndCompletionEvents()
    {
        using var listener = new AsyncGuardEventListener();

        await Task.CompletedTask.FireAndForget(taskName: "EventSourceSuccess");

        await AsyncTestHelpers.WaitFor(() =>
            listener.Events.Any(e => e.EventName == nameof(AsyncGuardEventSource.TaskCompleted)));

        Assert.Contains(listener.Events, e => e.EventName == nameof(AsyncGuardEventSource.TaskStarted));
        Assert.Contains(listener.Events, e => e.EventName == nameof(AsyncGuardEventSource.TaskCompleted));
    }

    [Fact]
    public async Task EmitsFailureEvent()
    {
        using var listener = new AsyncGuardEventListener();

        var guard = Task.Run(() => throw new InvalidOperationException("boom"))
            .FireAndForget(taskName: "EventSourceFailure");

        await guard;

        await AsyncTestHelpers.WaitFor(() =>
            listener.Events.Any(e => e.EventName == nameof(AsyncGuardEventSource.TaskFailed)));

        var failure = listener.Events.Last(e => e.EventName == nameof(AsyncGuardEventSource.TaskFailed));
        Assert.Equal("System.InvalidOperationException", failure.Payload?[2] as string);
    }

    [Fact]
    public async Task EmitsTimeoutEvent()
    {
        using var listener = new AsyncGuardEventListener();

        Func<Task> slow = async () => await Task.Delay(200);

        await slow.FireAndForget(taskName: "EventSourceTimeout", timeout: TimeSpan.FromMilliseconds(10));

        await AsyncTestHelpers.WaitFor(() =>
            listener.Events.Any(e => e.EventName == nameof(AsyncGuardEventSource.TaskTimeout)));

        Assert.Contains(listener.Events, e => e.EventName == nameof(AsyncGuardEventSource.TaskTimeout));
    }
}

internal sealed class AsyncGuardEventListener : EventListener
{
    private readonly List<EventWrittenEventArgs> _events = new();

    public IReadOnlyList<EventWrittenEventArgs> Events => _events;

    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        base.OnEventSourceCreated(eventSource);

        if (eventSource.Name == AsyncGuardEventSource.EventSourceName)
        {
            EnableEvents(eventSource, EventLevel.Verbose);
        }
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        lock (_events)
        {
            _events.Add(eventData);
        }
    }
}
