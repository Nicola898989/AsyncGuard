using Microsoft.Extensions.Logging;

namespace AsyncGuard.Templates;

public static class AsyncGuardConfig
{
    public static void ConfigureDefaults(ILogger? logger = null)
    {
        AsyncGuard.AsyncGuard.Configure(options =>
        {
            options.DefaultRetry = 2;
            options.DefaultTimeout = TimeSpan.FromSeconds(15);
            options.DefaultBackoff = BackoffStrategy.Linear;
            options.UseToonFormat = true;
            options.DefaultLogLevel = LogLevel.Information;
        });

        logger?.LogInformation("AsyncGuard configured with template defaults");
    }
}
