using System.Diagnostics;
using System.Diagnostics.Metrics;
using AsyncGuard.Internal;

namespace AsyncGuard;

/// <summary>
/// Exposes telemetry hooks that can be connected to OpenTelemetry or other monitoring pipelines.
/// </summary>
public static class AsyncGuardTelemetry
{
    /// <summary>
    /// Gets the <see cref="ActivitySource"/> used by AsyncGuard to emit spans.
    /// </summary>
    public static ActivitySource ActivitySource => AsyncGuardDiagnostics.ActivitySourceInstance;

    /// <summary>
    /// Gets the <see cref="Meter"/> used by AsyncGuard to emit metrics.
    /// </summary>
    public static Meter Meter => AsyncGuardDiagnostics.MeterInstance;
}
