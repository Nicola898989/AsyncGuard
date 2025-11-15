using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using AsyncGuard.Decorators;
using AsyncGuard.Tests.Infrastructure;

namespace AsyncGuard.Tests;

public class DecoratorTests
{
    public interface IJobService
    {
        Task RunAsync();
    }

    public interface IAttributedJobService
    {
        [AsyncGuardRetry(retryCount: 1, Backoff = BackoffStrategy.None)]
        Task RunAsync();
    }

    private sealed class FailingJobService : IJobService
    {
        private readonly Action _onInvoke;

        public FailingJobService(Action onInvoke)
        {
            _onInvoke = onInvoke;
        }

        [AsyncGuardRetry(retryCount: 1, Backoff = BackoffStrategy.Linear, TimeoutMilliseconds = 10)]
        public async Task RunAsync()
        {
            _onInvoke();
            await Task.Delay(10);
            throw new InvalidOperationException("boom");
        }
    }

    private sealed class InterfaceAttributedJobService : IAttributedJobService
    {
        private readonly Action _onInvoke;

        public InterfaceAttributedJobService(Action onInvoke)
        {
            _onInvoke = onInvoke;
        }

        public Task RunAsync()
        {
            _onInvoke();
            throw new InvalidOperationException("boom");
        }
    }

    [Fact]
    public async Task DecoratorAppliesFireAndForgetBehavior()
    {
        var logger = new TestLogger();
        var calls = 0;
        var service = new FailingJobService(() => calls++);
        var decorated = AsyncGuardDecorator.Create<IJobService>(service, logger);

        await decorated.RunAsync();

        await AsyncTestHelpers.WaitFor(() => logger.Entries.Any(e => e.Level == LogLevel.Error), 2000);
        Assert.True(calls >= 1);
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Error);
    }

    [Fact]
    public async Task DecoratorReadsAttributesFromInterface()
    {
        var logger = new TestLogger();
        var calls = 0;
        var service = new InterfaceAttributedJobService(() => calls++);
        var decorated = AsyncGuardDecorator.Create<IAttributedJobService>(service, logger);

        await decorated.RunAsync();

        await AsyncTestHelpers.WaitFor(() => logger.Entries.Any(e => e.Level == LogLevel.Error), 2000);
        Assert.True(calls >= 1);
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Error);
    }
}
