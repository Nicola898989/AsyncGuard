namespace AsyncGuard.Internal;

internal static class BackoffDelays
{
    public static TimeSpan GetDelay(BackoffStrategy strategy, int attempt, TimeSpan baseDelay)
    {
        if (baseDelay <= TimeSpan.Zero)
            baseDelay = TimeSpan.FromMilliseconds(500);

        return strategy switch
        {
            BackoffStrategy.Linear => Multiply(baseDelay, attempt),
            BackoffStrategy.Exponential => Multiply(baseDelay, Math.Pow(2, Math.Max(0, attempt - 1))),
            _ => baseDelay
        };
    }

    private static TimeSpan Multiply(TimeSpan baseDelay, double factor)
    {
        var ticks = baseDelay.Ticks * factor;
        if (ticks >= TimeSpan.MaxValue.Ticks)
            return TimeSpan.MaxValue;

        return TimeSpan.FromTicks((long)Math.Max(1, ticks));
    }
}
