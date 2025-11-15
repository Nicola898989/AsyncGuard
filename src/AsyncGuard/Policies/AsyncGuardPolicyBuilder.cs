namespace AsyncGuard.Policies;

public sealed class AsyncGuardPolicyBuilder
{
    private readonly List<PolicyRule> _rules = new();

    public AsyncGuardPolicyBuilder ForTask(string taskName, Action<AsyncGuardPolicy> configure)
    {
        if (string.IsNullOrWhiteSpace(taskName))
            throw new ArgumentException("Task name is required.", nameof(taskName));

        return ForPredicate(name => string.Equals(name, taskName, StringComparison.OrdinalIgnoreCase), configure);
    }

    public AsyncGuardPolicyBuilder ForPredicate(Func<string, bool> predicate, Action<AsyncGuardPolicy> configure)
    {
        if (predicate is null)
            throw new ArgumentNullException(nameof(predicate));

        if (configure is null)
            throw new ArgumentNullException(nameof(configure));

        var policy = new AsyncGuardPolicy();
        configure(policy);
        _rules.Add(new PolicyRule(predicate, policy));
        return this;
    }

    internal IReadOnlyList<PolicyRule> Build() => _rules.ToList();

    internal readonly record struct PolicyRule(Func<string, bool> Predicate, AsyncGuardPolicy Policy);
}
