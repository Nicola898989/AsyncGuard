using AsyncGuard.Pipeline;
using AsyncGuard.Tests.Infrastructure;

namespace AsyncGuard.Tests;

[Collection("AsyncGuardPipeline")]
public class PipelineResetTests
{
    public PipelineResetTests()
    {
        AsyncGuard.Reset();
        AsyncGuard.ResetPipeline();
    }

    [Fact]
    public async Task ResetRemovesPipelineHandlers()
    {
        var stages = new List<AsyncGuardPipelineStage>();
        AsyncGuard.ConfigurePipeline(builder =>
        {
            builder.OnStart(ctx =>
            {
                stages.Add(ctx.Stage);
                return Task.CompletedTask;
            });
        });

        AsyncGuard.Reset();

        var logger = new TestLogger();
        await Task.CompletedTask.FireAndForget(logger, "ResetPipelineJob");

        Assert.Empty(stages);
    }

    [Fact]
    public async Task UsePluginDoesNotOverrideExistingHandlers()
    {
        var stages = new List<AsyncGuardPipelineStage>();
        AsyncGuard.ConfigurePipeline(builder =>
        {
            builder.OnStart(ctx =>
            {
                stages.Add(ctx.Stage);
                return Task.CompletedTask;
            });
        });

        AsyncGuard.UsePlugin(new TestPlugin(ctx => stages.Add(ctx.Stage)));

        var logger = new TestLogger();
        var guard = Task.Run(() => throw new InvalidOperationException("boom"))
            .FireAndForget(logger, "PluginJob");
        await guard;

        Assert.Contains(AsyncGuardPipelineStage.Start, stages);
        Assert.Contains(AsyncGuardPipelineStage.Error, stages);
    }

    private sealed class TestPlugin : IAsyncGuardPlugin
    {
        private readonly Func<AsyncGuardPipelineContext, Task> _callback;

        public TestPlugin(Action<AsyncGuardPipelineContext> callback)
        {
            _callback = ctx =>
            {
                callback(ctx);
                return Task.CompletedTask;
            };
        }

        public void Configure(AsyncGuardPipelineBuilder builder)
        {
            builder.OnError(_callback);
        }
    }
}
