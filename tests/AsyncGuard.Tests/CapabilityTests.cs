using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Nodes;
using AsyncGuard.Tests.Infrastructure;
using Microsoft.Extensions.Logging;

namespace AsyncGuard.Tests;

public class CapabilityTests
{
    public CapabilityTests()
    {
        AsyncGuard.Reset();
    }

    [Fact]
    public async Task FireAndForget_DoesNotBlockCallingThread()
    {
        using var scope = AsyncGuard.Override(options =>
        {
            options.DefaultLogLevel = LogLevel.Information;
        });

        var logger = new TestLogger();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var guard = Task.Run(async () =>
        {
            started.SetResult();
            await release.Task;
        }).FireAndForget(logger, "BackgroundJob");

        await started.Task;
        Assert.False(guard.IsCompleted);

        release.SetResult();
        await guard;

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("BackgroundJob", entry.Message);
        Assert.Contains("Completed", entry.Message);
    }

    [Fact]
    public async Task Logging_IncludesDetailsOnSuccess()
    {
        using var scope = AsyncGuard.Override(options =>
        {
            options.DefaultLogLevel = LogLevel.Information;
        });

        var logger = new TestLogger();

        await Task.CompletedTask.FireAndForget(logger, "ReportExport");

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("ReportExport", entry.Message);
        Assert.Contains("Completed", entry.Message);
        Assert.Contains("1/1", entry.Message);
    }

    [Fact]
    public async Task NamedOperations_DefaultNameAppliedWhenMissing()
    {
        using var scope = AsyncGuard.Override(options =>
        {
            options.DefaultLogLevel = LogLevel.Information;
        });

        var logger = new TestLogger();
        Func<Task> operation = SampleOperationAsync;

        await operation.FireAndForget(logger);

        var entry = Assert.Single(logger.Entries);
        Assert.Contains(nameof(SampleOperationAsync), entry.Message);
    }

    [Fact]
    public async Task Timeout_DisabledWhenZero()
    {
        using var scope = AsyncGuard.Override(options =>
        {
            options.DefaultLogLevel = LogLevel.Information;
        });

        var logger = new TestLogger();

        Func<Task> slow = async () =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(60));
        };

        await slow.FireAndForget(logger, "NoTimeout", timeout: TimeSpan.Zero);

        var entry = Assert.Single(logger.Entries);
        Assert.Null(entry.Exception);
        Assert.Contains("Completed", entry.Message);
    }

    [Fact]
    public async Task Timeout_UsesDefaultOptionWhenNotProvided()
    {
        using var scope = AsyncGuard.Override(options =>
        {
            options.DefaultTimeout = TimeSpan.FromMilliseconds(20);
        });

        var logger = new TestLogger();

        Func<Task> slow = async () =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(200));
        };

        await slow.FireAndForget(logger, "DefaultTimeoutJob");

        var entry = Assert.Single(logger.Entries);
        Assert.Contains("DefaultTimeoutJob", entry.Message);
        Assert.Contains("Timeout", entry.Message);
        Assert.IsType<TimeoutException>(entry.Exception);
    }

    [Fact]
    public async Task Retry_UsesDefaultOptionWhenNotProvided()
    {
        using var scope = AsyncGuard.Override(options =>
        {
            options.DefaultRetry = 2;
        });

        var logger = new TestLogger();
        var attempts = 0;

        async Task Operation()
        {
            attempts++;

            if (attempts < 3)
                throw new InvalidOperationException($"Fail #{attempts}");
        }

        Func<Task> factory = Operation;

        await factory.FireAndForget(logger, "GlobalRetry");

        Assert.Equal(3, attempts);
        Assert.Equal(2, logger.Entries.Count(entry => entry.Level == LogLevel.Warning));
    }

    [Fact]
    public async Task Telemetry_RecordsActivityAndMetrics()
    {
        var logger = new TestLogger();
        using var telemetry = new TelemetryCollector();

        using (var parent = new Activity("Parent").Start())
        {
            parent.AddBaggage("tenant", "acme");
            await Task.CompletedTask.FireAndForget(logger, "TelemetryJob");
        }

        var activity = telemetry.Activities.Last(a =>
            a.DisplayName == "AsyncGuard.FireAndForget"
            && Equals(a.GetTagItem("asyncguard.task_name"), "TelemetryJob"));
        Assert.Equal(ActivityStatusCode.Ok, activity.Status);
        Assert.Equal("TelemetryJob", activity.GetTagItem("asyncguard.task_name"));
        Assert.Contains(activity.Baggage, kvp => kvp.Key == "tenant" && kvp.Value == "acme");

        Assert.Contains(telemetry.LongMeasurements, measurement =>
            measurement.Name == "asyncguard.operations.completed"
            && HasTag(measurement.Tags, "asyncguard.task_name", "TelemetryJob"));
    }

    [Fact]
    public async Task ToonLogging_ProducesDecodablePayload()
    {
        using var scope = AsyncGuard.Override(options =>
        {
            options.UseToonFormat = true;
            options.DefaultLogLevel = LogLevel.Information;
        });

        var logger = new TestLogger();

        await Task.CompletedTask.FireAndForget(logger, "ToonJob");

        var entry = Assert.Single(logger.Entries);
        var state = Assert.IsAssignableFrom<IReadOnlyList<KeyValuePair<string, object?>>>(entry.State);
        var payload = Assert.Single(state, kvp => kvp.Key == "Payload").Value as string;
        Assert.False(string.IsNullOrEmpty(payload));

        var decoded = ToonNet.ToonNet.Decode(payload!)!;
        Assert.Equal("ToonJob", decoded["TaskName"]?.GetValue<string>());
        Assert.Equal("Completed", decoded["Outcome"]?.GetValue<string>());
    }

    [Fact]
    public async Task DeveloperOptions_LogLevelNoneSuppressesSuccessLog()
    {
        using var scope = AsyncGuard.Override(options =>
        {
            options.DefaultLogLevel = LogLevel.None;
        });

        var logger = new TestLogger();

        await Task.CompletedTask.FireAndForget(logger, "SilentJob");

        Assert.Empty(logger.Entries);
    }

    private static Task SampleOperationAsync() => Task.CompletedTask;

    private static bool HasTag(IEnumerable<KeyValuePair<string, object?>> tags, string key, object? expected)
    {
        foreach (var tag in tags)
        {
            if (tag.Key == key && Equals(tag.Value, expected))
                return true;
        }

        return false;
    }
}
