using System.Collections.Generic;
using AsyncGuard.Tests.Infrastructure;
using AsyncGuard.Logging;
using Microsoft.Extensions.Logging;

namespace AsyncGuard.Tests;

public class FireAndForgetTests
{
    public FireAndForgetTests()
    {
        AsyncGuard.Reset();
    }

    [Fact]
    public async Task LogsErrorWhenTaskFails()
    {
        var logger = new TestLogger();

        var guard = Task.Run(async () =>
        {
            await Task.Delay(10);
            throw new InvalidOperationException("boom");
        }).FireAndForget(logger, "DataSync");

        await guard;

        var error = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, error.Level);
        Assert.Contains("DataSync", error.Message);
        Assert.IsType<InvalidOperationException>(error.Exception);
    }

    [Fact]
    public async Task RetriesFactoryUntilSuccess()
    {
        var logger = new TestLogger();
        var attempts = 0;

        async Task Operation()
        {
            attempts++;
            await Task.Delay(5);

            if (attempts < 3)
                throw new InvalidOperationException($"Fail #{attempts}");
        }

        Func<Task> factory = Operation;

        await factory.FireAndForget(
            logger,
            "PaymentProcessor",
            retryCount: 2,
            backoff: BackoffStrategy.Linear);

        Assert.Equal(3, attempts);
        Assert.Equal(2, logger.Entries.Count(entry => entry.Level == LogLevel.Warning));
    }

    [Fact]
    public async Task EmitsTimeoutAndInvokesErrorCallback()
    {
        var logger = new TestLogger();
        var captured = new List<Exception>();

        Func<Task> operation = async () =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(250));
        };

        var guard = operation.FireAndForget(
            logger,
            "HeavyJob",
            timeout: TimeSpan.FromMilliseconds(50),
            onError: captured.Add);

        await guard;

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Contains("HeavyJob", entry.Message);
        Assert.IsType<TimeoutException>(entry.Exception);
        var timeout = Assert.Single(captured);
        Assert.IsType<TimeoutException>(timeout);
    }

    [Fact]
    public async Task StopsMonitoringWhenTokenCancelled()
    {
        var logger = new TestLogger();
        using var cts = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Func<Task> operation = async () =>
        {
            started.SetResult();
            await Task.Delay(TimeSpan.FromSeconds(2));
        };

        var guard = operation.FireAndForget(
            logger,
            "LongJob",
            timeout: TimeSpan.FromSeconds(5),
            monitoringToken: cts.Token);

        await started.Task;
        cts.Cancel();

        await guard;

        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task UsesToonFormatWhenEnabled()
    {
        using var scope = AsyncGuard.Override(options =>
        {
            options.UseToonFormat = true;
            options.DefaultLogLevel = LogLevel.Information;
        });

        var logger = new TestLogger();

        await Task.CompletedTask.FireAndForget(logger, "EmailSender");

        var entry = Assert.Single(logger.Entries);
        Assert.Contains("TaskName", entry.Message);
        Assert.Contains("EmailSender", entry.Message);
        Assert.Null(entry.Exception);
    }

    [Fact]
    public async Task AggregateExceptionsAreUnwrappedBeforeLogging()
    {
        var logger = new TestLogger();

        var guard = Task.Run(() =>
        {
            throw new AggregateException(new InvalidOperationException("inner"));
        }).FireAndForget(logger, "AggregateJob");

        await guard;

        var entry = Assert.Single(logger.Entries);
        Assert.IsType<InvalidOperationException>(entry.Exception);
    }

    [Fact]
    public async Task UsesCustomAsyncGuardLoggerWhenProvided()
    {
        var logger = new FakeAsyncGuardLogger();

        var guard = Task.Run(() =>
        {
            throw new InvalidOperationException("boom");
        }).FireAndForget(logger);

        await guard;

        var entry = Assert.Single(logger.Entries);
        Assert.IsType<InvalidOperationException>(entry.Exception);
        Assert.Null(entry.TaskName);
    }
}
