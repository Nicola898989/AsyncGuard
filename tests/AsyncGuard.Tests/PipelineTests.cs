using System;
using AsyncGuard.Pipeline;
using AsyncGuard.Plugins;
using AsyncGuard.Tests.Infrastructure;

namespace AsyncGuard.Tests;

[Collection("AsyncGuardPipeline")]
public class PipelineTests
{
    public PipelineTests()
    {
        AsyncGuard.Reset();
        AsyncGuard.ResetPipeline();
        CapturedContexts.Clear();
    }

    [Fact]
    public async Task PipelineHandlersReceiveEvents()
    {
        var stages = new List<AsyncGuardPipelineStage>();
        AsyncGuard.ConfigurePipeline(builder =>
        {
            builder
                .OnStart(ctx =>
                {
                    stages.Add(ctx.Stage);
                    return Task.CompletedTask;
                })
                .OnRetry(ctx =>
                {
                    stages.Add(ctx.Stage);
                    return Task.CompletedTask;
                })
                .OnError(ctx =>
                {
                    stages.Add(ctx.Stage);
                    return Task.CompletedTask;
                })
                .OnComplete(ctx =>
                {
                    stages.Add(ctx.Stage);
                    return Task.CompletedTask;
                });
        });

        var attempts = 0;
        async Task Operation()
        {
            attempts++;
            if (attempts < 2)
                throw new InvalidOperationException("boom");
        }

        var logger = new TestLogger();
        await new Func<Task>(Operation).FireAndForget(logger, "PipelineJob", retryCount: 1);

        Assert.Contains(AsyncGuardPipelineStage.Start, stages);
        Assert.Contains(AsyncGuardPipelineStage.Retry, stages);
        Assert.Contains(AsyncGuardPipelineStage.Complete, stages);
    }

    [Fact]
    public async Task SuccessOnlyTriggersStartAndComplete()
    {
        var stages = new List<AsyncGuardPipelineStage>();
        AsyncGuard.ConfigurePipeline(builder =>
        {
            builder
                .OnStart(ctx =>
                {
                    stages.Add(ctx.Stage);
                    return Task.CompletedTask;
                })
                .OnRetry(ctx =>
                {
                    stages.Add(ctx.Stage);
                    return Task.CompletedTask;
                })
                .OnError(ctx =>
                {
                    stages.Add(ctx.Stage);
                    return Task.CompletedTask;
                })
                .OnComplete(ctx =>
                {
                    stages.Add(ctx.Stage);
                    return Task.CompletedTask;
                });
        });

        var logger = new TestLogger();
        await Task.CompletedTask.FireAndForget(logger, "PipelineSuccess");

        Assert.Equal(new[] { AsyncGuardPipelineStage.Start, AsyncGuardPipelineStage.Complete }, stages);
    }

    [Fact]
    public async Task PluginIsInvokedOnError()
    {
        AsyncGuard.ConfigurePipeline(builder => builder.AddPlugin(new SlackAsyncGuardPlugin(ctx =>
        {
            CapturedContexts.Add(ctx);
        })));

        var logger = new TestLogger();
        var contextsBefore = CapturedContexts.Count;

        var guard = Task.Run(() => throw new InvalidOperationException("boom"))
            .FireAndForget(logger, "PluginJob");

        await guard;

        Assert.True(CapturedContexts.Count > contextsBefore);
        Assert.Contains(CapturedContexts, ctx => ctx.Stage == AsyncGuardPipelineStage.Error);
    }

    private static readonly List<AsyncGuardPipelineContext> CapturedContexts = new();
}
