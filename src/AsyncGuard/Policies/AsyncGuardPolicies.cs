namespace AsyncGuard.Policies;

internal static class AsyncGuardPolicies
{
    private static readonly object Sync = new();
    private static IReadOnlyList<AsyncGuardPolicyBuilder.PolicyRule> _rules = Array.Empty<AsyncGuardPolicyBuilder.PolicyRule>();

    public static void Configure(Action<AsyncGuardPolicyBuilder> configure)
    {
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));

        lock (Sync)
        {
            var builder = new AsyncGuardPolicyBuilder();
            configure(builder);
            _rules = builder.Build();
        }
    }

    public static void Reset()
    {
        lock (Sync)
        {
            _rules = Array.Empty<AsyncGuardPolicyBuilder.PolicyRule>();
        }
    }

    public static AsyncGuardPolicy? Resolve(string taskName)
    {
        lock (Sync)
        {
            for (var i = _rules.Count - 1; i >= 0; i--)
            {
                var rule = _rules[i];
                if (rule.Predicate(taskName))
                    return rule.Policy;
            }
        }

        return null;
    }
}
