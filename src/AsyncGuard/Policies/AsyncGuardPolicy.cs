namespace AsyncGuard.Policies;

/// <summary>
/// Represents a per-task override for AsyncGuard behavior.
/// </summary>
public sealed class AsyncGuardPolicy
{
    public TimeSpan? Timeout { get; set; }

    public int? RetryCount { get; set; }

    public BackoffStrategy? Backoff { get; set; }
}
