using System;
using System.Collections.Generic;
using AsyncGuard.Tests.Infrastructure;
using TaskExtensionsMvp = AsyncGuard.Extensions.TaskExtensions;

namespace AsyncGuard.Tests;

public class MvpTaskExtensionsTests
{
    [Fact]
    public async Task FireAndForget_WithILogger_CapturesException()
    {
        var testLogger = new TestLogger();

        var task = Task.Run(() => throw new InvalidOperationException("boom"));
        TaskExtensionsMvp.FireAndForget(task, testLogger);

        await task.ContinueWith(_ => { });
        await AsyncTestHelpers.WaitFor(() => testLogger.Entries.Count == 1);

        var entry = Assert.Single(testLogger.Entries);
        Assert.IsType<InvalidOperationException>(entry.Exception);
    }

    [Fact]
    public async Task FireAndForget_WithCustomLogger_StoresEntries()
    {
        var logger = new FakeAsyncGuardLogger();

        var task = Task.Run(() => throw new InvalidOperationException("boom"));
        TaskExtensionsMvp.FireAndForget(task, "Job", logger);

        await task.ContinueWith(_ => { });
        await AsyncTestHelpers.WaitFor(() => logger.Entries.Count == 1);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal("Job", entry.TaskName);
        Assert.IsType<InvalidOperationException>(entry.Exception);
    }

    [Fact]
    public async Task FireAndForget_NoLogger_DoesNotThrow()
    {
        var task = Task.Run(async () => await Task.Delay(10));

        var exception = await Record.ExceptionAsync(() => Task.Run(() => TaskExtensionsMvp.FireAndForget(task)));

        Assert.Null(exception);
        await task;
    }

    [Fact]
    public async Task FireAndForget_UnwrapsAggregateExceptions()
    {
        var logger = new FakeAsyncGuardLogger();

        var task = Task.Run(() => throw new AggregateException(new InvalidOperationException("inner")));

        TaskExtensionsMvp.FireAndForget(task, logger);

        await task.ContinueWith(_ => { });
        await AsyncTestHelpers.WaitFor(() => logger.Entries.Count == 1);

        var entry = Assert.Single(logger.Entries);
        Assert.IsType<InvalidOperationException>(entry.Exception);
    }
}
