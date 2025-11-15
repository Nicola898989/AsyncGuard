namespace AsyncGuard.Pipeline;

public interface IAsyncGuardPlugin
{
    void Configure(AsyncGuardPipelineBuilder builder);
}
