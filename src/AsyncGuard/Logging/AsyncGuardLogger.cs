using Microsoft.Extensions.Logging;
using ToonNet;

namespace AsyncGuard.Logging;

internal static class AsyncGuardLogger
{
    public static void LogSuccess(ILogger logger, AsyncGuardOptions options, string taskName, int attempt, int maxAttempts, TimeSpan duration)
    {
        if (options.DefaultLogLevel == LogLevel.None)
            return;

        Log(logger, options, options.DefaultLogLevel, taskName, attempt, maxAttempts, duration, "Completed");
    }

    public static void LogFailure(
        ILogger logger,
        AsyncGuardOptions options,
        string taskName,
        int attempt,
        int maxAttempts,
        TimeSpan duration,
        string outcome,
        LogLevel level,
        Exception exception)
    {
        Log(logger, options, level, taskName, attempt, maxAttempts, duration, outcome, exception);
    }

    private static void Log(
        ILogger logger,
        AsyncGuardOptions options,
        LogLevel level,
        string taskName,
        int attempt,
        int maxAttempts,
        TimeSpan duration,
        string outcome,
        Exception? exception = null)
    {
        if (options.UseToonFormat)
        {
            var payload = new
            {
                TaskName = taskName,
                Outcome = outcome,
                Attempt = attempt,
                Attempts = maxAttempts,
                DurationMs = Math.Round(duration.TotalMilliseconds, 2),
                ExceptionType = exception?.GetType().FullName,
                ExceptionMessage = exception?.Message,
                StackTrace = exception?.StackTrace
            };

            var encoded = ToonNet.ToonNet.Encode(payload);
            logger.Log(level, exception, "AsyncGuard {Payload}", encoded);
            return;
        }

        if (exception is null)
        {
            logger.Log(
                level,
                "AsyncGuard task {TaskName} completed ({Outcome}) in {Duration} ms on attempt {Attempt}/{MaxAttempts}",
                taskName,
                outcome,
                duration.TotalMilliseconds,
                attempt,
                maxAttempts);
        }
        else
        {
            logger.Log(
                level,
                exception,
                "AsyncGuard task {TaskName} {Outcome} after {Duration} ms on attempt {Attempt}/{MaxAttempts}",
                taskName,
                outcome,
                duration.TotalMilliseconds,
                attempt,
                maxAttempts);
        }
    }
}
