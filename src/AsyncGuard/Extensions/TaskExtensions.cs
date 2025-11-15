using AsyncGuard.Logging;
using Microsoft.Extensions.Logging;

namespace AsyncGuard.Extensions;

/// <summary>
/// Minimal MVP extensions that execute fire-and-forget work using async void and guaranteed logging.
/// </summary>
public static class TaskExtensions
{
    public static void FireAndForget(this Task task, ILogger logger) => FireAndForget(task, null, logger);

    public static void FireAndForget(this Task task, string? taskName, ILogger logger)
    {
        if (logger is null)
            throw new ArgumentNullException(nameof(logger));

        FireAndForget(task, taskName, new DefaultAsyncGuardLogger(logger));
    }

    public static void FireAndForget(this Task task, IAsyncGuardLogger logger) => FireAndForget(task, null, logger);

    public static void FireAndForget(this Task task, string? taskName, IAsyncGuardLogger logger)
    {
        if (task is null)
            throw new ArgumentNullException(nameof(task));

        if (logger is null)
            throw new ArgumentNullException(nameof(logger));

        FireAndForgetInternal(task, taskName, logger);
    }

    public static void FireAndForget(this Task task)
    {
        if (task is null)
            throw new ArgumentNullException(nameof(task));

        FireAndForgetInternal(task, null, NoopAsyncGuardLogger.Instance);
    }

    private static async void FireAndForgetInternal(Task task, string? taskName, IAsyncGuardLogger logger)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.Log(Normalize(ex), taskName);
        }
    }

    private static Exception Normalize(Exception exception)
    {
        if (exception is AggregateException aggregate)
        {
            var flattened = aggregate.Flatten();

            if (flattened.InnerExceptions.Count == 1)
                return flattened.InnerExceptions[0];

            return flattened;
        }

        return exception;
    }
}
