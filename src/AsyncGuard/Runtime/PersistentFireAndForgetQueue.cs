using System.Collections.Concurrent;
using System.Text.Json;
using AsyncGuard.Internal;
using Microsoft.Extensions.Logging;

namespace AsyncGuard.Runtime;

/// <summary>
/// Simple persistent queue that stores fire-and-forget jobs to disk and replays them on restart.
/// </summary>
public sealed class PersistentFireAndForgetQueue : IAsyncDisposable, IDisposable
{
    private readonly PersistentFireAndForgetOptions _options;
    private readonly ILogger? _logger;
    private readonly ConcurrentDictionary<string, Func<PersistentJobContext, CancellationToken, Task>> _handlers = new();
    private readonly SemaphoreSlim _storageGate = new(1, 1);
    private readonly CancellationTokenSource _cts = new();
    private Task? _processingTask;

    public PersistentFireAndForgetQueue(PersistentFireAndForgetOptions? options = null, ILogger? logger = null)
    {
        _options = options ?? new PersistentFireAndForgetOptions();
        _logger = logger;
        var directory = Path.GetDirectoryName(_options.StoragePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        if (!File.Exists(_options.StoragePath))
        {
            File.WriteAllText(_options.StoragePath, "[]");
        }
    }

    public void RegisterHandler(string jobType, Func<PersistentJobContext, CancellationToken, Task> handler)
    {
        if (string.IsNullOrWhiteSpace(jobType))
            throw new ArgumentException("Job type is required", nameof(jobType));

        if (handler is null)
            throw new ArgumentNullException(nameof(handler));

        _handlers[jobType] = handler;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_processingTask is not null)
            return Task.CompletedTask;

        _processingTask = Task.Run(ProcessLoopAsync, _cts.Token);
        return Task.CompletedTask;
    }

    public async Task EnqueueAsync<TPayload>(string jobType, TPayload payload, CancellationToken cancellationToken = default)
    {
        var record = new PersistentJobRecord
        {
            Id = Guid.NewGuid(),
            JobType = jobType,
            Payload = JsonSerializer.Serialize(payload),
            EnqueuedAt = DateTimeOffset.UtcNow,
            Attempts = 0
        };

        await AppendRecordAsync(record, cancellationToken).ConfigureAwait(false);
    }

    private async Task ProcessLoopAsync()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                var job = await DequeueRecordAsync(_cts.Token).ConfigureAwait(false);
                if (job is null)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(250), _cts.Token).ConfigureAwait(false);
                    continue;
                }

                if (!_handlers.TryGetValue(job.JobType, out var handler))
                {
                    _logger?.LogWarning("No handler registered for job type {JobType}. Job {JobId} skipped.", job.JobType, job.Id);
                    continue;
                }

                var payloadElement = JsonDocument.Parse(job.Payload).RootElement.Clone();
                var context = new PersistentJobContext(job.Id, job.JobType, job.EnqueuedAt, payloadElement);

                try
                {
                    await handler(context, _cts.Token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    job.Attempts++;
                    if (job.Attempts < _options.MaxAttempts)
                    {
                        var delay = BackoffDelays.GetDelay(_options.Backoff, job.Attempts, _options.RetryBaseDelay);
                        await Task.Delay(delay, _cts.Token).ConfigureAwait(false);
                        await AppendRecordAsync(job, _cts.Token).ConfigureAwait(false);
                        _logger?.LogWarning(ex, "Persistent job {JobId} requeued (attempt {Attempts})", job.Id, job.Attempts);
                        continue;
                    }

                    _logger?.LogError(ex, "Persistent job {JobId} failed after {Attempts} attempts", job.Id, job.Attempts);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Persistent queue loop error");
                await Task.Delay(TimeSpan.FromSeconds(1), _cts.Token).ConfigureAwait(false);
            }
        }
    }

    private async Task AppendRecordAsync(PersistentJobRecord record, CancellationToken cancellationToken)
    {
        await _storageGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var records = await ReadRecordsAsync(cancellationToken).ConfigureAwait(false);
            records.Add(record);
            await WriteRecordsAsync(records, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _storageGate.Release();
        }
    }

    private async Task<PersistentJobRecord?> DequeueRecordAsync(CancellationToken cancellationToken)
    {
        await _storageGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var records = await ReadRecordsAsync(cancellationToken).ConfigureAwait(false);
            if (records.Count == 0)
                return null;

            var record = records[0];
            records.RemoveAt(0);
            await WriteRecordsAsync(records, cancellationToken).ConfigureAwait(false);
            return record;
        }
        finally
        {
            _storageGate.Release();
        }
    }

    private async Task<List<PersistentJobRecord>> ReadRecordsAsync(CancellationToken cancellationToken)
    {
        using var stream = File.Open(_options.StoragePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await JsonSerializer.DeserializeAsync<List<PersistentJobRecord>>(stream, cancellationToken: cancellationToken).ConfigureAwait(false)
               ?? new List<PersistentJobRecord>();
    }

    private async Task WriteRecordsAsync(List<PersistentJobRecord> records, CancellationToken cancellationToken)
    {
        using var stream = File.Open(_options.StoragePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, records, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        if (_processingTask is not null)
        {
            try
            {
                await _processingTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _storageGate.Dispose();
        _cts.Dispose();
    }

    public void Dispose()
    {
        DisposeAsync().GetAwaiter().GetResult();
    }

    private sealed class PersistentJobRecord
    {
        public Guid Id { get; set; }
        public string JobType { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
        public DateTimeOffset EnqueuedAt { get; set; }
        public int Attempts { get; set; }
    }
}
