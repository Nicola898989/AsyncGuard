using AsyncGuard.Policies;
using AsyncGuard.Tests.Infrastructure;
using Microsoft.Extensions.Logging;

namespace AsyncGuard.Tests;

public class PolicyTests
{
    public PolicyTests()
    {
        AsyncGuard.Reset();
    }

    [Fact]
    public async Task PolicyOverridesTimeoutAndRetry()
    {
        AsyncGuard.ConfigurePolicies(builder =>
        {
            builder.ForTask("PolicyJob", policy =>
            {
                policy.Timeout = TimeSpan.FromMilliseconds(10);
                policy.RetryCount = 0;
                policy.Backoff = BackoffStrategy.None;
            });
        });

        var logger = new TestLogger();
        Func<Task> slow = async () => await Task.Delay(200);

        await slow.FireAndForget(logger, "PolicyJob");

        var entry = Assert.Single(logger.Entries);
        Assert.IsType<TimeoutException>(entry.Exception);
    }

    [Fact]
    public async Task PolicyCanDisableTimeout()
    {
        using var scope = AsyncGuard.Override(options =>
        {
            options.DefaultLogLevel = LogLevel.None;
        });
        AsyncGuard.ConfigurePolicies(builder =>
        {
            builder.ForTask("PolicyNoTimeout", policy =>
            {
                policy.Timeout = TimeSpan.Zero;
            });
        });

        var logger = new TestLogger();
        Func<Task> slow = async () => await Task.Delay(100);

        await slow.FireAndForget(logger, "PolicyNoTimeout");

        Assert.DoesNotContain(logger.Entries, entry => entry.Exception is TimeoutException);
    }

    [Fact]
    public async Task LastMatchingRuleWins()
    {
        AsyncGuard.ConfigurePolicies(builder =>
        {
            builder.ForTask("RuleJob", policy => policy.Timeout = TimeSpan.FromSeconds(5));
            builder.ForTask("RuleJob", policy => policy.Timeout = TimeSpan.FromMilliseconds(10));
        });

        var logger = new TestLogger();
        Func<Task> slow = async () => await Task.Delay(200);

        await slow.FireAndForget(logger, "RuleJob");

        var entry = Assert.Single(logger.Entries);
        Assert.IsType<TimeoutException>(entry.Exception);
    }
}
