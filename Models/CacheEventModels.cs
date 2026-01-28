namespace CacheClient.Models;

/// <summary>
/// Types of cache events that can be raised.
/// </summary>
public enum CacheEventType
{
    ItemAdded,
    ItemUpdated,
    ItemRemoved,
    ItemExpired,
    ItemEvicted
}

/// <summary>
/// Represents a cache event notification.
/// </summary>
public sealed class CacheEvent
{
    public CacheEventType EventType { get; set; }
    public string Key { get; set; }
    public object Value { get; set; }
    public DateTime Timestamp { get; set; }
    public string Reason { get; set; }
}

/// <summary>
/// Event arguments for cache events.
/// </summary>
public sealed class CacheEventArgs : EventArgs
{
    public CacheEventType EventType { get; }
    public string Key { get; }
    public object Value { get; }
    public DateTime Timestamp { get; }
    public string Reason { get; }

    public CacheEventArgs(CacheEvent cacheEvent)
    {
        EventType = cacheEvent.EventType;
        Key = cacheEvent.Key;
        Value = cacheEvent.Value;
        Timestamp = cacheEvent.Timestamp;
        Reason = cacheEvent.Reason;
    }

    public CacheEventArgs(CacheEventType eventType, string key, object value = null, string reason = null)
    {
        EventType = eventType;
        Key = key;
        Value = value;
        Timestamp = DateTime.UtcNow;
        Reason = reason;
    }
}
