using Microsoft.Extensions.Logging;

namespace AsyncGuard;

/// <summary>
/// Global options that control AsyncGuard behavior.
/// </summary>
public class AsyncGuardOptions
{
    /// <summary>
    /// Gets or sets the default timeout applied when none is specified. Use <see cref="TimeSpan.Zero"/> to disable the timeout.
    /// </summary>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the default retry count (number of re-executions after the first attempt).
    /// </summary>
    public int DefaultRetry { get; set; }

    /// <summary>
    /// Gets or sets the default backoff strategy used between retries.
    /// </summary>
    public BackoffStrategy DefaultBackoff { get; set; } = BackoffStrategy.None;

    /// <summary>
    /// Gets or sets the log level used for successful executions.
    /// </summary>
    public LogLevel DefaultLogLevel { get; set; } = LogLevel.Error;

    /// <summary>
    /// Gets or sets a value indicating whether structured TOON logs should be emitted.
    /// </summary>
    public bool UseToonFormat { get; set; }

    /// <summary>
    /// Gets or sets the base delay used to compute retry backoff windows.
    /// </summary>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromMilliseconds(500);

    internal AsyncGuardOptions Clone()
    {
        return new AsyncGuardOptions
        {
            DefaultTimeout = DefaultTimeout,
            DefaultRetry = DefaultRetry,
            DefaultBackoff = DefaultBackoff,
            DefaultLogLevel = DefaultLogLevel,
            UseToonFormat = UseToonFormat,
            RetryBaseDelay = RetryBaseDelay
        };
    }
}
