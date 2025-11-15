# AsyncGuard

AsyncGuard keeps every fire-and-forget Task in your .NET services safe: logging, retry/backoff, timeout, policy overrides, persistent queue, scheduler, decorators, telemetry, Roslyn analyzer, pipeline hooks and plugins—all with a single `.FireAndForget()` call.

## Highlights

- **Fire-and-forget API** with overloads for `Task`, `Task<T>`, `Func<Task>`, `Func<CancellationToken, Task>`, optional logger, timeout, retry, backoff, `onError` and cancellation support.
- **Observability**: structured logs (also TOON-encoded), OpenTelemetry spans/metrics, ETW events, pipeline hooks for custom alerts.
- **Runtime helpers**: scheduler, persistent queue with recovery, decorator + `[AsyncGuardRetry]`, global policies.
- **Developer experience**: Roslyn analyzer (AG0001) + code fix, ready-to-use templates/dashboard, plugin system.

## Getting started

```csharp
await SomeBackgroundWork()
    .FireAndForget(
        logger,
        taskName: "BackgroundWork",
        timeout: TimeSpan.FromSeconds(10),
        retryCount: 3,
        backoff: BackoffStrategy.Exponential,
        onError: ex => metrics.TrackFailure(ex));
```

See the full README on GitHub for pipelines, persistent queue, scheduler, decorator, analyzer and plugin examples.
