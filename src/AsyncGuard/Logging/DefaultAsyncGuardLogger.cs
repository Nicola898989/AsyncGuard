using Microsoft.Extensions.Logging;

namespace AsyncGuard.Logging;

/// <summary>
/// Simple adapter that forwards <see cref="IAsyncGuardLogger"/> calls to <see cref="ILogger"/>.
/// </summary>
public sealed class DefaultAsyncGuardLogger : IAsyncGuardLogger
{
    private readonly ILogger _logger;

    public DefaultAsyncGuardLogger(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Log(Exception exception, string? taskName = null)
    {
        if (exception is null)
            throw new ArgumentNullException(nameof(exception));

        _logger.LogError(exception, "AsyncGuard captured background exception for task {TaskName}", taskName ?? "AsyncGuardTask");
    }
}
