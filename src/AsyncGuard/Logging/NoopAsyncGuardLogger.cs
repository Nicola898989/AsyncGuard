namespace AsyncGuard.Logging;

/// <summary>
/// Logger that discards all entries. Useful for the no-op MVP overload.
/// </summary>
public sealed class NoopAsyncGuardLogger : IAsyncGuardLogger
{
    public static NoopAsyncGuardLogger Instance { get; } = new();

    private NoopAsyncGuardLogger()
    {
    }

    public void Log(Exception exception, string? taskName = null)
    {
        // intentionally left blank
    }
}
