namespace AsyncGuard.Pipeline;

public enum AsyncGuardPipelineStage
{
    Start,
    Retry,
    Error,
    Complete
}
