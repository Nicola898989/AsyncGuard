namespace AsyncGuard.Runtime;

public sealed class PersistentFireAndForgetOptions
{
    public string StoragePath { get; set; } = Path.Combine(Path.GetTempPath(), "asyncguard.jobs.json");

    public int MaxAttempts { get; set; } = 3;

    public BackoffStrategy Backoff { get; set; } = BackoffStrategy.Exponential;

    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(1);
}
