using System.Diagnostics;
using System.Diagnostics.Metrics;
using AsyncGuard.Logging;
using AsyncGuard.Pipeline;
using Microsoft.Extensions.Logging;

namespace AsyncGuard.Internal;

internal static class AsyncGuardRunner
{
    public static Task RunAsync(
        AsyncGuardOperation operation,
        ILogger? logger,
        string? taskName,
        TimeSpan? timeout,
        int? retryCount,
        BackoffStrategy? backoff,
        Action<Exception>? onError,
        CancellationToken monitoringToken)
    {
        if (operation is null)
            throw new ArgumentNullException(nameof(operation));

        var resolvedLogger = AsyncGuard.ResolveLogger(logger);
        var options = AsyncGuard.Snapshot();

        var resolvedName = string.IsNullOrWhiteSpace(taskName) ? operation.Name : taskName!;
        var effectiveTimeout = ResolveTimeout(timeout, options.DefaultTimeout);
        var effectiveRetryCount = ResolveRetryCount(retryCount, options.DefaultRetry);
        var effectiveBackoff = backoff ?? options.DefaultBackoff;
        var policy = Policies.AsyncGuardPolicies.Resolve(resolvedName);
        if (policy is not null)
        {
            if (policy.Timeout.HasValue)
                effectiveTimeout = policy.Timeout <= TimeSpan.Zero ? null : policy.Timeout;
            if (policy.RetryCount.HasValue)
                effectiveRetryCount = Math.Max(0, policy.RetryCount.Value);
            if (policy.Backoff.HasValue)
                effectiveBackoff = policy.Backoff.Value;
        }

        if (!operation.SupportsRetry)
        {
            if (effectiveRetryCount > 0)
            {
                resolvedLogger.LogDebug(
                    "AsyncGuard retries were requested for task {TaskName}, but the provided Task instance cannot be re-executed.",
                    resolvedName);
            }

            effectiveRetryCount = 0;
        }

        var pipeline = Pipeline.AsyncGuardPipeline.Snapshot();

        var args = new AsyncGuardExecutionArguments(
            resolvedLogger,
            resolvedName,
            effectiveTimeout,
            effectiveRetryCount,
            effectiveBackoff,
            onError,
            options,
            monitoringToken,
            pipeline);

        return Task.Run(() => ExecuteAsync(operation, args), CancellationToken.None);
    }

    private static async Task ExecuteAsync(AsyncGuardOperation operation, AsyncGuardExecutionArguments args)
    {
        var totalAttempts = operation.SupportsRetry ? args.RetryCount + 1 : 1;

        for (var attempt = 1; attempt <= totalAttempts; attempt++)
        {
            if (args.MonitoringToken.IsCancellationRequested)
                return;

            var tags = AsyncGuardDiagnostics.CreateTags(args.TaskName, attempt);
            AsyncGuardEventSource.Log.TaskStarted(args.TaskName, attempt);
            AsyncGuardDiagnostics.RecordStart(tags);
            await InvokePipelineAsync(args.Pipeline.OnStart, args.TaskName, attempt, totalAttempts, AsyncGuardPipelineStage.Start).ConfigureAwait(false);

            var parentActivity = Activity.Current;
            var parentContext = parentActivity?.Context ?? default;
            using var activity = AsyncGuardDiagnostics.StartActivity(args.TaskName, attempt, parentContext, parentActivity);
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var task = operation.StartAsync(CancellationToken.None);
                var outcome = await ObserveAsync(task, args).ConfigureAwait(false);

                if (outcome == OperationOutcome.Completed)
                {
                    stopwatch.Stop();
                    activity?.SetStatus(ActivityStatusCode.Ok);
                    AsyncGuardDiagnostics.RecordDuration(stopwatch.Elapsed, tags);
                    AsyncGuardDiagnostics.RecordCompletion(tags);
                    AsyncGuardLogger.LogSuccess(args.Logger, args.Options, args.TaskName, attempt, totalAttempts, stopwatch.Elapsed);
                    await InvokePipelineAsync(args.Pipeline.OnComplete, args.TaskName, attempt, totalAttempts, AsyncGuardPipelineStage.Complete, stopwatch.Elapsed).ConfigureAwait(false);
                    AsyncGuardEventSource.Log.TaskCompleted(args.TaskName, attempt, stopwatch.Elapsed.TotalMilliseconds, totalAttempts);
                    return;
                }

                if (outcome == OperationOutcome.MonitoringCancelled)
                {
                    AsyncGuardDiagnostics.RecordCancellation(tags);
                    return;
                }

                // Timeout case
                stopwatch.Stop();
                var timeoutException = new TimeoutException(
                    $"AsyncGuard detected that task '{args.TaskName}' timed out after {args.Timeout?.TotalMilliseconds ?? 0} ms.");
                args.OnError?.Invoke(timeoutException);
                activity?.SetStatus(ActivityStatusCode.Error, timeoutException.Message);
                AsyncGuardDiagnostics.RecordTimeout(tags);
                AsyncGuardLogger.LogFailure(
                    args.Logger,
                    args.Options,
                    args.TaskName,
                    attempt,
                    totalAttempts,
                    stopwatch.Elapsed,
                    "Timeout",
                    LogLevel.Error,
                    timeoutException);
                AsyncGuardEventSource.Log.TaskTimeout(args.TaskName, attempt, stopwatch.Elapsed.TotalMilliseconds, totalAttempts);
                await InvokePipelineAsync(args.Pipeline.OnError, args.TaskName, attempt, totalAttempts, AsyncGuardPipelineStage.Error, stopwatch.Elapsed, timeoutException).ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (!args.MonitoringToken.IsCancellationRequested)
            {
                stopwatch.Stop();
                var normalized = NormalizeException(ex);
                args.OnError?.Invoke(normalized);

                var isFinalAttempt = attempt == totalAttempts;
                var outcome = isFinalAttempt ? "Failed" : "Retry";

                activity?.SetStatus(ActivityStatusCode.Error, normalized.Message);

                if (isFinalAttempt)
                {
                    AsyncGuardDiagnostics.RecordFailure(tags);
                    AsyncGuardLogger.LogFailure(
                        args.Logger,
                        args.Options,
                        args.TaskName,
                        attempt,
                        totalAttempts,
                        stopwatch.Elapsed,
                        outcome,
                        LogLevel.Error,
                        normalized);
                    await InvokePipelineAsync(args.Pipeline.OnError, args.TaskName, attempt, totalAttempts, AsyncGuardPipelineStage.Error, stopwatch.Elapsed, normalized).ConfigureAwait(false);
                    AsyncGuardEventSource.Log.TaskFailed(args.TaskName, attempt, normalized.GetType().FullName ?? normalized.GetType().Name, totalAttempts);
                    return;
                }

                AsyncGuardDiagnostics.RecordRetry(tags);
                AsyncGuardLogger.LogFailure(
                    args.Logger,
                    args.Options,
                    args.TaskName,
                    attempt,
                    totalAttempts,
                    stopwatch.Elapsed,
                    outcome,
                    LogLevel.Warning,
                    normalized);
                AsyncGuardEventSource.Log.TaskFailed(args.TaskName, attempt, normalized.GetType().FullName ?? normalized.GetType().Name, totalAttempts);
                await InvokePipelineAsync(args.Pipeline.OnRetry, args.TaskName, attempt, totalAttempts, AsyncGuardPipelineStage.Retry, stopwatch.Elapsed, normalized).ConfigureAwait(false);

                var delay = BackoffDelays.GetDelay(args.Backoff, attempt, args.Options.RetryBaseDelay);
                try
                {
                    await Task.Delay(delay, args.MonitoringToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    AsyncGuardDiagnostics.RecordCancellation(tags);
                    return;
                }
            }
        }
    }

    private static async Task<OperationOutcome> ObserveAsync(Task task, AsyncGuardExecutionArguments args)
    {
        if (args.Timeout is null || args.Timeout <= TimeSpan.Zero)
        {
            await task.ConfigureAwait(false);
            return OperationOutcome.Completed;
        }

        var timeoutTask = Task.Delay(args.Timeout.Value, CancellationToken.None);
        Task? cancellationTask = null;

        if (args.MonitoringToken.CanBeCanceled)
            cancellationTask = Task.Delay(Timeout.InfiniteTimeSpan, args.MonitoringToken);

        Task completedTask;

        if (cancellationTask is null)
        {
            completedTask = await Task.WhenAny(task, timeoutTask).ConfigureAwait(false);
        }
        else
        {
            completedTask = await Task.WhenAny(task, timeoutTask, cancellationTask).ConfigureAwait(false);
        }

        if (completedTask == task)
        {
            await task.ConfigureAwait(false);
            return OperationOutcome.Completed;
        }

        if (cancellationTask != null && completedTask == cancellationTask)
        {
            ObserveInBackground(task, args);
            return OperationOutcome.MonitoringCancelled;
        }

        ObserveInBackground(task, args);
        return OperationOutcome.Timeout;
    }

    private static void ObserveInBackground(Task task, AsyncGuardExecutionArguments args)
    {
#pragma warning disable AG0001 // L'osservazione in background è intenzionalmente fire-and-forget
        _ = task.ContinueWith(
            t =>
            {
                if (t.IsFaulted && t.Exception is not null)
                {
                    args.Logger.LogDebug(
                        t.Exception,
                        "AsyncGuard task {TaskName} completed after detaching from background monitoring.",
                        args.TaskName);
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
#pragma warning restore AG0001
    }

    private static TimeSpan? ResolveTimeout(TimeSpan? timeout, TimeSpan defaultTimeout)
    {
        if (timeout.HasValue)
        {
            if (timeout <= TimeSpan.Zero)
                return null;

            return timeout;
        }

        if (defaultTimeout <= TimeSpan.Zero)
            return null;

        return defaultTimeout;
    }

    private static int ResolveRetryCount(int? retryCount, int defaultRetry)
    {
        if (retryCount.HasValue)
            return Math.Max(0, retryCount.Value);

        return Math.Max(0, defaultRetry);
    }

    private enum OperationOutcome
    {
        Completed,
        Timeout,
        MonitoringCancelled
    }

    private readonly record struct AsyncGuardExecutionArguments(
        ILogger Logger,
        string TaskName,
        TimeSpan? Timeout,
        int RetryCount,
        BackoffStrategy Backoff,
        Action<Exception>? OnError,
        AsyncGuardOptions Options,
        CancellationToken MonitoringToken,
        AsyncGuardPipelineBuilder.PipelineConfiguration Pipeline);

    private static Task InvokePipelineAsync(
        Func<AsyncGuardPipelineContext, Task>[] handlers,
        string taskName,
        int attempt,
        int totalAttempts,
        AsyncGuardPipelineStage stage,
        TimeSpan? duration = null,
        Exception? exception = null)
    {
        if (handlers.Length == 0)
            return Task.CompletedTask;

        var context = new AsyncGuardPipelineContext
        {
            TaskName = taskName,
            Attempt = attempt,
            TotalAttempts = totalAttempts,
            Stage = stage,
            Duration = duration,
            Exception = exception
        };

        return Pipeline.AsyncGuardPipeline.InvokeAsync(handlers, context);
    }

    private static Exception NormalizeException(Exception exception)
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
