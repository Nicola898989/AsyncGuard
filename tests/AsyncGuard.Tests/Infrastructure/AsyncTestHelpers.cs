using System;

namespace AsyncGuard.Tests.Infrastructure;

internal static class AsyncTestHelpers
{
    public static async Task WaitFor(Func<bool> predicate, int timeoutMs = 1000)
    {
        var start = Environment.TickCount64;

        while (!predicate())
        {
            if (Environment.TickCount64 - start > timeoutMs)
                break;

            await Task.Delay(10);
        }
    }
}
