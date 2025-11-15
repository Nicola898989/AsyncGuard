using System.IO;
using AsyncGuard.Runtime;
using AsyncGuard.Tests.Infrastructure;

namespace AsyncGuard.Tests;

public class PersistentQueueTests
{
    [Fact]
    public async Task ProcessesEnqueuedJobs()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"asyncguard-test-{Guid.NewGuid()}.json");
        var options = new PersistentFireAndForgetOptions
        {
            StoragePath = tempFile
        };

        await using var queue = new PersistentFireAndForgetQueue(options);
        var processed = 0;
        queue.RegisterHandler("test", async (ctx, token) =>
        {
            processed += ctx.DeserializePayload<int>();
            await Task.CompletedTask;
        });

        await queue.StartAsync();
        await queue.EnqueueAsync("test", 1);

        await AsyncTestHelpers.WaitFor(() => processed == 1, 2000);
        Assert.Equal(1, processed);
        if (File.Exists(tempFile))
            File.Delete(tempFile);
    }

    [Fact]
    public async Task RetriesOnFailure()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"asyncguard-test-{Guid.NewGuid()}.json");
        var options = new PersistentFireAndForgetOptions
        {
            StoragePath = tempFile,
            MaxAttempts = 2,
            RetryBaseDelay = TimeSpan.FromMilliseconds(10)
        };

        await using var queue = new PersistentFireAndForgetQueue(options);
        var attempts = 0;
        queue.RegisterHandler("flaky", (ctx, token) =>
        {
            attempts++;
            if (attempts == 1)
                throw new InvalidOperationException("boom");
            return Task.CompletedTask;
        });

        await queue.StartAsync();
        await queue.EnqueueAsync("flaky", new { value = 1 });

        await AsyncTestHelpers.WaitFor(() => attempts >= 2, 2000);
        Assert.Equal(2, attempts);
        if (File.Exists(tempFile))
            File.Delete(tempFile);
    }

    [Fact]
    public async Task StopsRetryingAfterMaxAttempts()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"asyncguard-test-{Guid.NewGuid()}.json");
        var options = new PersistentFireAndForgetOptions
        {
            StoragePath = tempFile,
            MaxAttempts = 1,
            RetryBaseDelay = TimeSpan.FromMilliseconds(10)
        };

        await using var queue = new PersistentFireAndForgetQueue(options);
        var attempts = 0;
        queue.RegisterHandler("alwaysFail", (ctx, token) =>
        {
            attempts++;
            throw new InvalidOperationException("fail");
        });

        await queue.StartAsync();
        await queue.EnqueueAsync("alwaysFail", new { value = 1 });

        await AsyncTestHelpers.WaitFor(() => attempts >= 1, 2000);
        Assert.Equal(1, attempts);
        if (File.Exists(tempFile))
            File.Delete(tempFile);
    }

    [Fact]
    public async Task StartAsyncHonorsCancellationToken()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"asyncguard-test-{Guid.NewGuid()}.json");
        var options = new PersistentFireAndForgetOptions
        {
            StoragePath = tempFile
        };

        await using var queue = new PersistentFireAndForgetQueue(options);
        var processed = 0;
        queue.RegisterHandler("noop", (ctx, token) =>
        {
            processed++;
            return Task.CompletedTask;
        });

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await queue.StartAsync(cts.Token);

        await queue.EnqueueAsync("noop", new { });

        await Task.Delay(200);
        Assert.Equal(0, processed);
        if (File.Exists(tempFile))
            File.Delete(tempFile);
    }
}
