using AsyncGuard.Internal;
using AsyncGuard.Logging;
using Microsoft.Extensions.Logging;

namespace AsyncGuard;

/// <summary>
/// Provides the FireAndForget extension methods.
/// </summary>
public static class FireAndForgetExtensions
{
    public static Task FireAndForget(
        this Task task,
        ILogger? logger = null,
        string? taskName = null,
        TimeSpan? timeout = null,
        int? retryCount = null,
        BackoffStrategy? backoff = null,
        Action<Exception>? onError = null,
        CancellationToken monitoringToken = default)
    {
        if (task is null)
            throw new ArgumentNullException(nameof(task));

        var operation = AsyncGuardOperation.FromTask(task, taskName);
        return AsyncGuardRunner.RunAsync(operation, logger, taskName, timeout, retryCount, backoff, onError, monitoringToken);
    }

    public static Task FireAndForget(this Task task, ILogger logger)
    {
        if (task is null)
            throw new ArgumentNullException(nameof(task));

        if (logger is null)
            throw new ArgumentNullException(nameof(logger));

        return FireAndForget(task, logger, null, null, null, null, null, default);
    }

    public static Task FireAndForget(this Task task, string taskName, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(taskName))
            throw new ArgumentException("Task name must be provided.", nameof(taskName));

        return FireAndForget(task, logger ?? throw new ArgumentNullException(nameof(logger)), taskName, null, null, null, null, default);
    }

    public static Task FireAndForget(this Task task, IAsyncGuardLogger asyncLogger)
    {
        if (task is null)
            throw new ArgumentNullException(nameof(task));

        if (asyncLogger is null)
            throw new ArgumentNullException(nameof(asyncLogger));

        return task.FireAndForget(taskName: null, asyncLogger: asyncLogger);
    }

    public static Task FireAndForget(this Task task, string? taskName, IAsyncGuardLogger asyncLogger)
    {
        if (task is null)
            throw new ArgumentNullException(nameof(task));

        if (asyncLogger is null)
            throw new ArgumentNullException(nameof(asyncLogger));

        return FireAndForget(
            task,
            (ILogger?)null,
            taskName,
            timeout: null,
            retryCount: null,
            backoff: null,
            onError: exception => asyncLogger.Log(exception, taskName),
            monitoringToken: default);
    }

    public static Task FireAndForget(this Task task)
    {
        if (task is null)
            throw new ArgumentNullException(nameof(task));

        return FireAndForget(task, (ILogger?)null, null, null, null, null, null, default);
    }

    public static Task FireAndForget<T>(
        this Task<T> task,
        ILogger? logger = null,
        string? taskName = null,
        TimeSpan? timeout = null,
        int? retryCount = null,
        BackoffStrategy? backoff = null,
        Action<Exception>? onError = null,
        CancellationToken monitoringToken = default)
    {
        if (task is null)
            throw new ArgumentNullException(nameof(task));

        return ((Task)task).FireAndForget(logger, taskName, timeout, retryCount, backoff, onError, monitoringToken);
    }

    public static Task FireAndForget(
        this Func<Task> taskFactory,
        ILogger? logger = null,
        string? taskName = null,
        TimeSpan? timeout = null,
        int? retryCount = null,
        BackoffStrategy? backoff = null,
        Action<Exception>? onError = null,
        CancellationToken monitoringToken = default)
    {
        if (taskFactory is null)
            throw new ArgumentNullException(nameof(taskFactory));

        var operation = AsyncGuardOperation.FromDelegate(taskFactory, taskName);
        return AsyncGuardRunner.RunAsync(operation, logger, taskName, timeout, retryCount, backoff, onError, monitoringToken);
    }

    public static Task FireAndForget(
        this Func<CancellationToken, Task> taskFactory,
        ILogger? logger = null,
        string? taskName = null,
        TimeSpan? timeout = null,
        int? retryCount = null,
        BackoffStrategy? backoff = null,
        Action<Exception>? onError = null,
        CancellationToken monitoringToken = default)
    {
        if (taskFactory is null)
            throw new ArgumentNullException(nameof(taskFactory));

        var operation = AsyncGuardOperation.FromDelegate(taskFactory, taskName);
        return AsyncGuardRunner.RunAsync(operation, logger, taskName, timeout, retryCount, backoff, onError, monitoringToken);
    }
}
