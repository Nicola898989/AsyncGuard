namespace AsyncGuard.Pipeline;

public sealed class AsyncGuardPipelineBuilder
{
    private readonly List<Func<AsyncGuardPipelineContext, Task>> _onStart = new();
    private readonly List<Func<AsyncGuardPipelineContext, Task>> _onRetry = new();
    private readonly List<Func<AsyncGuardPipelineContext, Task>> _onError = new();
    private readonly List<Func<AsyncGuardPipelineContext, Task>> _onComplete = new();

    public AsyncGuardPipelineBuilder OnStart(Func<AsyncGuardPipelineContext, Task> handler)
    {
        _onStart.Add(handler ?? throw new ArgumentNullException(nameof(handler)));
        return this;
    }

    public AsyncGuardPipelineBuilder OnRetry(Func<AsyncGuardPipelineContext, Task> handler)
    {
        _onRetry.Add(handler ?? throw new ArgumentNullException(nameof(handler)));
        return this;
    }

    public AsyncGuardPipelineBuilder OnError(Func<AsyncGuardPipelineContext, Task> handler)
    {
        _onError.Add(handler ?? throw new ArgumentNullException(nameof(handler)));
        return this;
    }

    public AsyncGuardPipelineBuilder OnComplete(Func<AsyncGuardPipelineContext, Task> handler)
    {
        _onComplete.Add(handler ?? throw new ArgumentNullException(nameof(handler)));
        return this;
    }

    internal PipelineConfiguration Build()
    {
        return new PipelineConfiguration(
            _onStart.ToArray(),
            _onRetry.ToArray(),
            _onError.ToArray(),
            _onComplete.ToArray());
    }

    public AsyncGuardPipelineBuilder AddPlugin(IAsyncGuardPlugin plugin)
    {
        if (plugin is null)
            throw new ArgumentNullException(nameof(plugin));

        plugin.Configure(this);
        return this;
    }

    internal readonly record struct PipelineConfiguration(
        Func<AsyncGuardPipelineContext, Task>[] OnStart,
        Func<AsyncGuardPipelineContext, Task>[] OnRetry,
        Func<AsyncGuardPipelineContext, Task>[] OnError,
        Func<AsyncGuardPipelineContext, Task>[] OnComplete)
    {
        public static PipelineConfiguration Empty { get; } = new(Array.Empty<Func<AsyncGuardPipelineContext, Task>>(), Array.Empty<Func<AsyncGuardPipelineContext, Task>>(), Array.Empty<Func<AsyncGuardPipelineContext, Task>>(), Array.Empty<Func<AsyncGuardPipelineContext, Task>>());
    }
}
