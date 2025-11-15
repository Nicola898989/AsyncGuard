using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace AsyncGuard.Decorators;

public static class AsyncGuardDecorator
{
    public static TInterface Create<TInterface>(TInterface implementation, ILogger? logger = null)
        where TInterface : class
    {
        if (!typeof(TInterface).IsInterface)
            throw new InvalidOperationException("AsyncGuardDecorator works with interfaces only.");

        var proxy = DispatchProxy.Create<TInterface, AsyncGuardProxy>();
        ((AsyncGuardProxy)(object)proxy!).Initialize(implementation, logger);
        return proxy!;
    }

    private class AsyncGuardProxy : DispatchProxy
    {
        private object? _implementation;
        private ILogger? _logger;

        public void Initialize(object implementation, ILogger? logger)
        {
            _implementation = implementation ?? throw new ArgumentNullException(nameof(implementation));
            _logger = logger;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod is null || _implementation is null)
                throw new InvalidOperationException("Proxy not initialized correctly.");

            var attr = GetAttribute(targetMethod);
            if (attr is not null && typeof(Task).IsAssignableFrom(targetMethod.ReturnType))
            {
                Func<Task> factory = () =>
                {
                    var result = (Task?)targetMethod.Invoke(_implementation, args) ?? Task.CompletedTask;
                    return result;
                };

                var timeout = attr.TimeoutMilliseconds > 0 ? TimeSpan.FromMilliseconds(attr.TimeoutMilliseconds) : (TimeSpan?)null;
                return factory.FireAndForget(
                    _logger,
                    attr.TaskName ?? targetMethod.Name,
                    timeout,
                    attr.RetryCount,
                    attr.Backoff);
            }

            try
            {
                return targetMethod.Invoke(_implementation, args);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                throw ex.InnerException;
            }
        }

        private AsyncGuardRetryAttribute? GetAttribute(MethodInfo interfaceMethod)
        {
            var attr = interfaceMethod.GetCustomAttribute<AsyncGuardRetryAttribute>();
            if (attr is not null)
                return attr;

            var parameters = interfaceMethod.GetParameters().Select(p => p.ParameterType).ToArray();
            var implementationMethod = _implementation!.GetType().GetMethod(interfaceMethod.Name, parameters);
            return implementationMethod?.GetCustomAttribute<AsyncGuardRetryAttribute>();
        }
    }
}
