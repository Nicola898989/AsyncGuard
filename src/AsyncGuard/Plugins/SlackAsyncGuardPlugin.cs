using AsyncGuard.Pipeline;

namespace AsyncGuard.Plugins;

/// <summary>
/// Sample plugin that invokes a callback for every AsyncGuard error.
/// </summary>
public sealed class SlackAsyncGuardPlugin : IAsyncGuardPlugin
{
    private readonly Action<AsyncGuardPipelineContext> _callback;

    public SlackAsyncGuardPlugin(Action<AsyncGuardPipelineContext> callback)
    {
        _callback = callback ?? throw new ArgumentNullException(nameof(callback));
    }

    public void Configure(AsyncGuardPipelineBuilder builder)
    {
        builder.OnError(context =>
        {
            _callback(context);
            return Task.CompletedTask;
        });
    }
}
