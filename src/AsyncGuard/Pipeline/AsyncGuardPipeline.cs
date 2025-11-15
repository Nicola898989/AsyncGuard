namespace AsyncGuard.Pipeline;

internal static class AsyncGuardPipeline
{
    private static readonly object Sync = new();
    private static AsyncGuardPipelineBuilder.PipelineConfiguration _configuration = AsyncGuardPipelineBuilder.PipelineConfiguration.Empty;

    public static void Configure(Action<AsyncGuardPipelineBuilder> configure)
    {
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));

        lock (Sync)
        {
            var builder = new AsyncGuardPipelineBuilder();
            configure(builder);
            _configuration = builder.Build();
        }
    }

    public static AsyncGuardPipelineBuilder.PipelineConfiguration Snapshot()
    {
        lock (Sync)
        {
            return _configuration;
        }
    }

    public static void Reset()
    {
        lock (Sync)
        {
            _configuration = AsyncGuardPipelineBuilder.PipelineConfiguration.Empty;
        }
    }

    public static async Task InvokeAsync(Func<AsyncGuardPipelineContext, Task>[] handlers, AsyncGuardPipelineContext context)
    {
        if (handlers.Length == 0)
            return;

        foreach (var handler in handlers)
        {
            await handler(context).ConfigureAwait(false);
        }
    }
}
