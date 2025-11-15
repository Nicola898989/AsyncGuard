using System.Diagnostics.CodeAnalysis;

namespace AsyncGuard.Internal;

internal sealed class AsyncGuardOperation
{
    private readonly Func<CancellationToken, Task> _factory;

    private AsyncGuardOperation(Func<CancellationToken, Task> factory, string name, bool supportsRetry)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        Name = name;
        SupportsRetry = supportsRetry;
    }

    public string Name { get; }

    public bool SupportsRetry { get; }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var task = _factory(cancellationToken);
        if (task is null)
            throw new InvalidOperationException("The provided operation returned a null Task.");

        return task;
    }

    public static AsyncGuardOperation FromTask(Task task, string? requestedName)
    {
        if (task is null)
            throw new ArgumentNullException(nameof(task));

        var consumed = false;
        var gate = new object();
        var name = AsyncGuardNameResolver.FromTask(requestedName, task);

        return new AsyncGuardOperation(
            _ =>
            {
                lock (gate)
                {
                    if (consumed)
                        throw new InvalidOperationException("The provided Task instance was already consumed and cannot be retried.");

                    consumed = true;
                }

                return task;
            },
            name,
            supportsRetry: false);
    }

    public static AsyncGuardOperation FromDelegate(Func<Task> operation, string? requestedName)
    {
        if (operation is null)
            throw new ArgumentNullException(nameof(operation));

        var name = AsyncGuardNameResolver.FromDelegate(requestedName, operation);

        return new AsyncGuardOperation(_ => Invoke(operation), name, supportsRetry: true);
    }

    public static AsyncGuardOperation FromDelegate(Func<CancellationToken, Task> operation, string? requestedName)
    {
        if (operation is null)
            throw new ArgumentNullException(nameof(operation));

        var name = AsyncGuardNameResolver.FromDelegate(requestedName, operation);

        return new AsyncGuardOperation(operation, name, supportsRetry: true);
    }

    private static Task Invoke(Func<Task> operation)
    {
        try
        {
            return operation();
        }
        catch (Exception ex)
        {
            return Task.FromException(ex);
        }
    }
}
