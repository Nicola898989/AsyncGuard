using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using AsyncGuard.Policies;
using AsyncGuard.Pipeline;

namespace AsyncGuard;

/// <summary>
/// Entry point used to configure AsyncGuard.
/// </summary>
public static class AsyncGuard
{
    private static readonly object Sync = new();
    private static AsyncGuardOptions _options = new();

    /// <summary>
    /// Gets a snapshot of the current default options.
    /// </summary>
    public static AsyncGuardOptions Defaults
    {
        get
        {
            lock (Sync)
            {
                return _options.Clone();
            }
        }
    }

    /// <summary>
    /// Applies the provided configuration delegate to the global defaults.
    /// </summary>
    public static void Configure(Action<AsyncGuardOptions> configure)
    {
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));

        lock (Sync)
        {
            var clone = _options.Clone();
            configure(clone);
            Validate(clone);
            _options = clone;
        }
    }

    /// <summary>
    /// Applies the provided configuration for the duration of the returned disposable scope.
    /// </summary>
    public static IDisposable Override(Action<AsyncGuardOptions> configure)
    {
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));

        AsyncGuardOptions snapshot;

        lock (Sync)
        {
            snapshot = _options.Clone();
            var clone = snapshot.Clone();
            configure(clone);
            Validate(clone);
            _options = clone;
        }

        return new RestoreScope(snapshot);
    }

    /// <summary>
    /// Resets the defaults to the initial values.
    /// </summary>
    public static void Reset()
    {
        lock (Sync)
        {
            _options = new AsyncGuardOptions();
        }
        AsyncGuardPolicies.Reset();
        AsyncGuardPipeline.Reset();
    }

    internal static AsyncGuardOptions Snapshot()
    {
        lock (Sync)
        {
            return _options.Clone();
        }
    }

    internal static ILogger ResolveLogger(ILogger? logger) => logger ?? NullLogger.Instance;

    private static void Validate(AsyncGuardOptions options)
    {
        if (options.DefaultRetry < 0)
            throw new ArgumentOutOfRangeException(nameof(options.DefaultRetry), "Retry count cannot be negative.");

        if (options.DefaultTimeout < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options.DefaultTimeout), "Timeout must be non-negative.");

        if (options.RetryBaseDelay <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options.RetryBaseDelay), "Retry base delay must be positive.");
    }

    private sealed class RestoreScope : IDisposable
    {
        private AsyncGuardOptions? _snapshot;

        public RestoreScope(AsyncGuardOptions snapshot)
        {
            _snapshot = snapshot;
        }

        public void Dispose()
        {
            if (_snapshot is null)
                return;

            lock (Sync)
            {
                _options = _snapshot;
            }

            _snapshot = null;
        }
    }

    /// <summary>
    /// Configures global policies that can override timeout/retry/backoff per task name.
    /// </summary>
    public static void ConfigurePolicies(Action<AsyncGuardPolicyBuilder> configure)
    {
        AsyncGuardPolicies.Configure(configure);
    }

    /// <summary>
    /// Configures the AsyncGuard pipeline (onStart/onRetry/onError/onComplete).
    /// </summary>
    public static void ConfigurePipeline(Action<AsyncGuardPipelineBuilder> configure)
    {
        AsyncGuardPipeline.Configure(configure);
    }

    /// <summary>
    /// Resets the pipeline to the default empty configuration.
    /// </summary>
    public static void ResetPipeline()
    {
        AsyncGuardPipeline.Reset();
    }

    /// <summary>
    /// Registers a pipeline plugin.
    /// </summary>
    public static void UsePlugin(IAsyncGuardPlugin plugin)
    {
        if (plugin is null)
            throw new ArgumentNullException(nameof(plugin));

        var snapshot = AsyncGuardPipeline.Snapshot();
        ConfigurePipeline(builder =>
        {
            foreach (var handler in snapshot.OnStart)
                builder.OnStart(handler);
            foreach (var handler in snapshot.OnRetry)
                builder.OnRetry(handler);
            foreach (var handler in snapshot.OnError)
                builder.OnError(handler);
            foreach (var handler in snapshot.OnComplete)
                builder.OnComplete(handler);

            builder.AddPlugin(plugin);
        });
    }
}
