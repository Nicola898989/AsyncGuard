namespace AsyncGuard.Pipeline;

public sealed class AsyncGuardPipelineContext
{
    public required string TaskName { get; init; }
    public required int Attempt { get; init; }
    public required int TotalAttempts { get; init; }
    public AsyncGuardPipelineStage Stage { get; init; }
    public TimeSpan? Duration { get; init; }
    public Exception? Exception { get; init; }
}
