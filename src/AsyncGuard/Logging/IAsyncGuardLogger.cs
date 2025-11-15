namespace AsyncGuard.Logging;

/// <summary>
/// Minimal logging interface used by the MVP API to capture fire-and-forget failures.
/// </summary>
public interface IAsyncGuardLogger
{
    /// <summary>
    /// Logs the provided exception and optional task name.
    /// </summary>
    void Log(Exception exception, string? taskName = null);
}
