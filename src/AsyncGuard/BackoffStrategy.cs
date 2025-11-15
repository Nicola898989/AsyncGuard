namespace AsyncGuard;

/// <summary>
/// Defines the available retry backoff strategies.
/// </summary>
public enum BackoffStrategy
{
    None,
    Linear,
    Exponential
}
