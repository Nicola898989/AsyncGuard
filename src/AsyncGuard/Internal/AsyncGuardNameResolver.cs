namespace AsyncGuard.Internal;

internal static class AsyncGuardNameResolver
{
    private const string DefaultName = "AsyncGuardTask";

    public static string FromTask(string? requestedName, Task task)
    {
        if (!string.IsNullOrWhiteSpace(requestedName))
            return requestedName!;

        if (task.AsyncState is string state && !string.IsNullOrWhiteSpace(state))
            return state;

        return task.GetType().Name;
    }

    public static string FromDelegate(string? requestedName, Delegate? operation)
    {
        if (!string.IsNullOrWhiteSpace(requestedName))
            return requestedName!;

        if (operation?.Method?.Name is { Length: > 0 } methodName && methodName != "<Main>")
            return methodName;

        if (operation?.Target is not null)
            return operation.Target.GetType().Name;

        return DefaultName;
    }
}
