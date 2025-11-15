namespace AsyncGuard.Decorators;

[AttributeUsage(AttributeTargets.Method)]
public sealed class AsyncGuardRetryAttribute : Attribute
{
    public AsyncGuardRetryAttribute(int retryCount = 0)
    {
        RetryCount = retryCount;
    }

    public int RetryCount { get; }

    public BackoffStrategy Backoff { get; set; } = BackoffStrategy.None;

    public int TimeoutMilliseconds { get; set; }

    public string? TaskName { get; set; }
}
