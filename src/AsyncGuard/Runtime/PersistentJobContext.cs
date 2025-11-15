using System.Text.Json;

namespace AsyncGuard.Runtime;

public sealed class PersistentJobContext
{
    private readonly JsonElement _payload;

    internal PersistentJobContext(Guid id, string jobType, DateTimeOffset enqueuedAt, JsonElement payload)
    {
        Id = id;
        JobType = jobType;
        EnqueuedAt = enqueuedAt;
        _payload = payload;
    }

    public Guid Id { get; }

    public string JobType { get; }

    public DateTimeOffset EnqueuedAt { get; }

    public JsonElement Payload => _payload;

    public T DeserializePayload<T>()
    {
        return _payload.Deserialize<T>()!;
    }
}
