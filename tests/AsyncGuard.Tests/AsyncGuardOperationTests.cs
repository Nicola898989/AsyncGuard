using AsyncGuard.Internal;

namespace AsyncGuard.Tests;

public class AsyncGuardOperationTests
{
    [Fact]
    public async Task FromTaskCannotBeReused()
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation = AsyncGuardOperation.FromTask(tcs.Task, "Reusable");

        var first = operation.StartAsync(CancellationToken.None);
        Assert.Equal(tcs.Task, first);
        tcs.SetResult();
        await first;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await operation.StartAsync(CancellationToken.None);
        });
        Assert.Contains("already consumed", ex.Message);
    }

    [Fact]
    public void FromDelegateUsesMethodNameWhenTaskNameMissing()
    {
        var operation = AsyncGuardOperation.FromDelegate(SampleAsync, null);
        Assert.Equal(nameof(SampleAsync), operation.Name);
    }

    private static Task SampleAsync() => Task.CompletedTask;
}
