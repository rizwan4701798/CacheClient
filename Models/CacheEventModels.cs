namespace CacheClient.Models;

public enum CacheEventType
{
    ItemAdded,
    ItemUpdated,
    ItemRemoved,
    ItemExpired,
    ItemEvicted
}

public sealed class CacheEvent
{
    public CacheEventType EventType { get; set; }
    public string Key { get; set; } = string.Empty;
    public object? Value { get; set; }
    public DateTime Timestamp { get; set; }
    public string? Reason { get; set; }
}

public sealed class CacheEventArgs(CacheEvent cacheEvent) : EventArgs
{
    public CacheEventType EventType { get; } = cacheEvent.EventType;
    public string Key { get; } = cacheEvent.Key;
    public object? Value { get; } = cacheEvent.Value;
    public DateTime Timestamp { get; } = cacheEvent.Timestamp;
    public string? Reason { get; } = cacheEvent.Reason;

    public CacheEventArgs(CacheEventType eventType, string key, object? value = null, string? reason = null)
        : this(new CacheEvent
        {
            EventType = eventType,
            Key = key,
            Value = value,
            Timestamp = DateTime.UtcNow,
            Reason = reason
        })
    {
    }
}
