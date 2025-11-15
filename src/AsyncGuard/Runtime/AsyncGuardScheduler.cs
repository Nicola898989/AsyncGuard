using System.Threading;
using AsyncGuard;
using Microsoft.Extensions.Logging;

namespace AsyncGuard.Runtime;

/// <summary>
/// Provides a lightweight scheduler to execute recurring fire-and-forget tasks using AsyncGuard.
/// </summary>
public sealed class AsyncGuardScheduler : IDisposable
{
    private readonly List<ScheduledTask> _tasks = new();
    private bool _disposed;

    public IDisposable Schedule(
        Func<Task> taskFactory,
        TimeSpan every,
        string? taskName = null,
        ILogger? logger = null)
    {
        if (taskFactory is null)
            throw new ArgumentNullException(nameof(taskFactory));

        if (every <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(every));

        var scheduledTask = new ScheduledTask(taskFactory, every, taskName, logger);
        lock (_tasks)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AsyncGuardScheduler));

            _tasks.Add(scheduledTask);
        }

        return scheduledTask;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        lock (_tasks)
        {
            if (_disposed)
                return;

            foreach (var task in _tasks.ToArray())
                task.Dispose();

            _tasks.Clear();
            _disposed = true;
        }
    }

    private sealed class ScheduledTask : IDisposable
    {
        private readonly Func<Task> _factory;
        private readonly ILogger? _logger;
        private readonly string _taskName;
        private readonly Timer _timer;

        public ScheduledTask(Func<Task> factory, TimeSpan interval, string? taskName, ILogger? logger)
        {
            _factory = factory;
            _logger = logger;
            _taskName = taskName ?? "ScheduledTask";
            _timer = new Timer(Callback, null, TimeSpan.Zero, interval);
        }

        private void Callback(object? state)
        {
            try
            {
                var task = _factory();
                task.FireAndForget(_logger, _taskName);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "AsyncGuardScheduler failed to dispatch task {TaskName}", _taskName);
            }
        }

        public void Dispose()
        {
            _timer.Dispose();
        }
    }
}
