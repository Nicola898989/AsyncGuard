# AsyncGuard

AsyncGuard is a battle-ready guardian for every background Task you launch in .NET. Born from countless microservices gone rogue, it standardizes fire-and-forget work with production-grade safety nets: logging, telemetry, retry/backoff, policy overrides, persistent storage, schedulers, decorators, analyzers, and a plugin pipeline. If your app ever kicked off a Task and prayed nothing exploded, AsyncGuard finally gives you deterministic outcomes, complete observability, and the confidence to scale.

> AsyncGuard captures every Task you release, wraps it in guardrails, and tells the whole story across logs, traces, metrics, policies, and alerts.

## Why AsyncGuard exists

Modern services launch dozens of background jobs: queue consumers, cache warmers, email senders, payment retries… yet most of them still rely on `async void` or `_ = SomeTask()` and hope for the best. The result?

- Exceptions disappear with zero diagnostics.
- Thread pool starvation and deadlocks creep in.
- Operational teams have no clue when background jobs fail.
- Developers re-implement timeout/retry logic in every repo.

**AsyncGuard** solves this once and for all. Drop in a fluent API, add the Roslyn analyzer to your solution, and every background Task instantly gains logging, metrics, tracing, retry policies, and plugin hooks.

## Product tour

| Capability | What it fixes | What you get |
|------------|---------------|--------------|
| Fire-and-forget extensions | `_ = Task()` hacks, `async void`, forgotten Tasks | Fluent overloads for `Task`, `Task<T>`, `Func<Task>`, `Func<CancellationToken, Task>` with ILogger/IAsyncGuardLogger, timeout, retry/backoff, `onError`, cancellation monitoring, policy overrides. One-liner safety nets with zero boilerplate. |
| Structured logging + TOON | Logs that say “Failed” without context | Task name, human-readable attempt count, duration, exception type/message/stack, retry counters, timeout flags, plus optional TOON payloads to slash log size (perfect for ingestion-limited platforms). |
| Timeout + retry + backoff | Jobs that hang forever or crash silently | Per-call and per-policy timeout enforcement, linear/exponential backoff, aggregated retries with log levels (Warning for retriable errors, Error for final failure). |
| Persistent queue | Jobs lost during restarts or transient failures | File-based durable queue with JSON payloads, handler registry, automatic restart recovery, configurable max attempts/backoff, built-in telemetry/logging. |
| Scheduler | Cron-like jobs without hosted services | `AsyncGuardScheduler` with `Schedule(() => Job(), every: TimeSpan)` |
| Decorators & attributes | Boilerplate around services | `[AsyncGuardRetry]` attribute + interface proxy to enforce guard patterns automatically |
| Global policies | Inconsistent settings per team | `AsyncGuard.ConfigurePolicies(...)` overrides for specific task names |
| OpenTelemetry + EventSource | Broken traces | ActivitySource + Meter instrumentation, baggage propagation, ETW events |
| Roslyn analyzer + CodeFix | Hidden debt in code | Warning **AG0001** for un-awaited Tasks + one-click `.FireAndForget()` fix |
| Pipeline & plugin system | Custom alerting needs | Hook into OnStart/OnRetry/OnError/OnComplete and ship Slack/Sentry/blob plugins |
| Templates & dashboards | Slow onboarding | Ready-made `AsyncGuardConfig.cs`, `asyncguard.json`, and Kusto dashboards for failure/timeout heatmaps |

## Installation

```bash
# within /AsyncGuard
dotnet build AsyncGuard.sln
```

Add a project reference or pack the library, then import the `AsyncGuard` namespace. To enable analyzer + code-fix, reference the `AsyncGuard.Analyzers` package (already part of the solution) in your consuming projects.

## Quick start

```csharp
await ProcessPaymentsAsync()
    .FireAndForget(
        logger,
        taskName: "ProcessPayments",
        timeout: TimeSpan.FromSeconds(10),
        retryCount: 3,
        backoff: BackoffStrategy.Exponential,
        onError: ex => metrics.TrackFailure(ex));
```

Need the classic MVP overloads?

```csharp
await SomeTask();                    // normal flow
SomeTask().FireAndForget(logger);     // ILogger mandatory
SomeTask().FireAndForget();           // noop logger
SomeTask().FireAndForget(customLog);  // custom IAsyncGuardLogger
```

## Global configuration

```csharp
AsyncGuard.Configure(options =>
{
    options.DefaultTimeout = TimeSpan.FromSeconds(30);
    options.DefaultRetry = 1;
    options.DefaultBackoff = BackoffStrategy.Linear;
    options.UseToonFormat = true;
    options.DefaultLogLevel = LogLevel.Information;
});

AsyncGuard.ConfigurePolicies(builder =>
{
    builder.ForTask("CriticalJob", policy =>
    {
        policy.Timeout = TimeSpan.FromSeconds(5);
        policy.RetryCount = 3;
        policy.Backoff = BackoffStrategy.Exponential;
    });
});

AsyncGuard.ConfigurePipeline(builder =>
{
    builder.OnStart(ctx =>
    {
        logger.LogInformation("{Task} attempt {Attempt}", ctx.TaskName, ctx.Attempt);
        return Task.CompletedTask;
    })
    .OnError(ctx =>
    {
        alerting.Notify(ctx.TaskName, ctx.Exception);
        return Task.CompletedTask;
    })
    .AddPlugin(new SlackAsyncGuardPlugin(ctx => SlackNotify(ctx.TaskName, ctx.Exception)));
});
```

## Runtime helpers

### Scheduler
```csharp
using var scheduler = new AsyncGuardScheduler();
using var handle = scheduler.Schedule(
    () => emailService.SendDigestAsync(),
    every: TimeSpan.FromMinutes(15),
    taskName: "DigestSender",
    logger);
```

### Persistent queue
```csharp
var queue = new PersistentFireAndForgetQueue(new PersistentFireAndForgetOptions
{
    StoragePath = Path.Combine(AppContext.BaseDirectory, "asyncguard.jobs.json")
});

queue.RegisterHandler("SendEmail", async (ctx, token) =>
{
    var payload = ctx.DeserializePayload<EmailPayload>();
    await emailService.SendAsync(payload);
});

await queue.StartAsync();
await queue.EnqueueAsync("SendEmail", new EmailPayload { Address = "user@example.com" });
```

### Decorator + attribute
```csharp
public interface IJobService { Task RunAsync(); }

public class JobService : IJobService
{
    [AsyncGuardRetry(retryCount: 2, Backoff = BackoffStrategy.Linear, TimeoutMilliseconds = 5000)]
    public Task RunAsync() => DoRiskyStuffAsync();
}

IJobService guarded = AsyncGuardDecorator.Create<IJobService>(new JobService(), logger);
await guarded.RunAsync();
```

## Telemetry + tracing

AsyncGuard exposes `AsyncGuardTelemetry.ActivitySource` / `AsyncGuardTelemetry.Meter`. Add them to OpenTelemetry and (optionally) Azure Monitor:

```csharp
services.AddOpenTelemetry()
    .WithTracing(b => b.AddSource(AsyncGuardTelemetry.ActivitySource.Name))
    .WithMetrics(b => b.AddMeter(AsyncGuardTelemetry.Meter.Name))
    .UseAzureMonitor();
```

Baggage from `Activity.Current` is copied to every AsyncGuard span, so distributed traces stay intact.

## Analyzer & CodeFix

- **AG0001** warns when a Task is invoked without awaiting or `FireAndForget()`.
- The code-fix wraps the call with `.FireAndForget()` and adds `using AsyncGuard;` automatically.

## Templates & dashboards

The `templates/` folder ships:
- `AsyncGuardConfig.cs` – copy/paste to bootstrap default options in new services.
- `asyncguard.json` – config seed for pipelines/backoff.
- `kusto-dashboard.kql` – failure trend, timeout analysis, and retry heatmap queries ready for Azure Data Explorer/Fabric dashboards.

## Roadmap (all shipped!)

- ✅ **1.0** MVP: base API, logging, analyzer-ready tests.
- ✅ **1.5** Guarding: timeout, retry/backoff, cancellation monitoring.
- ✅ **2.0** Logging/Telemetry: TOON, OpenTelemetry, EventSource.
- ✅ **2.5** Developer experience: analyzer, code-fix, templates.
- ✅ **3.0** Runtime: scheduler, persistent queue, decorator proxy.
- ✅ **3.5** Enterprise: policy engine, baggage propagation, Kusto dashboards.
- ✅ **4.0** AsyncGuard PRO: guard pipeline + plugin system.

## Contributing / feedback

- Run `dotnet test AsyncGuard.sln` before sending PRs (30 tests cover the runtime pipeline).
- Filed issues and feature requests are welcome—just describe the background job scenario you’re trying to tame.
- Have a notification provider (Slack, Sentry, Teams, PagerDuty) or storage backend to contribute? Implement `IAsyncGuardPlugin` or extend the persistent queue and send a PR.

AsyncGuard is the insurance policy your background jobs always needed. Plug it in, forget about `_ = Task()`, and ship safer .NET services.
