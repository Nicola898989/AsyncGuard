using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading;
using AsyncGuard.Runtime;

namespace AsyncGuard.Tests;

public class SchedulerTests
{
    [Fact]
    public async Task ExecutesTaskAtInterval()
    {
        using var scheduler = new AsyncGuardScheduler();
        var count = 0;
        using var handle = scheduler.Schedule(() =>
        {
            Interlocked.Increment(ref count);
            return Task.CompletedTask;
        }, TimeSpan.FromMilliseconds(20), "SchedulerTest");

        await Task.Delay(120);

        Assert.True(count >= 2);
    }

    [Fact]
    public async Task DisposingHandleStopsInvocations()
    {
        using var scheduler = new AsyncGuardScheduler();
        var count = 0;
        var handle = scheduler.Schedule(() =>
        {
            Interlocked.Increment(ref count);
            return Task.CompletedTask;
        }, TimeSpan.FromMilliseconds(20), "StopTest");

        await Task.Delay(60);
        handle.Dispose();
        var captured = count;

        await Task.Delay(60);
        Assert.Equal(captured, count);
    }

    [Fact]
    public void ThrowsIfIntervalIsZeroOrNegative()
    {
        using var scheduler = new AsyncGuardScheduler();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            scheduler.Schedule(() => Task.CompletedTask, TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            scheduler.Schedule(() => Task.CompletedTask, TimeSpan.FromMilliseconds(-1)));
        Assert.Throws<ArgumentNullException>(() =>
            scheduler.Schedule(null!, TimeSpan.FromMilliseconds(10)));
    }

    [Fact]
    public void DisposedHandleIsRemovedFromScheduler()
    {
        using var scheduler = new AsyncGuardScheduler();
        var handle = scheduler.Schedule(() => Task.CompletedTask, TimeSpan.FromMilliseconds(50));

        handle.Dispose();

        var field = typeof(AsyncGuardScheduler).GetField("_tasks", BindingFlags.NonPublic | BindingFlags.Instance);
        var list = (IEnumerable?)field?.GetValue(scheduler) ?? Array.Empty<object>();

        Assert.Empty(list.Cast<object>());
    }
}
