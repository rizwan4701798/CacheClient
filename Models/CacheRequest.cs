namespace CacheClient.Models;

public sealed class CacheRequest
{
    public CacheOperation Operation { get; set; }
    public string? Key { get; set; }
    public object? Value { get; set; }
    public int? ExpirationSeconds { get; set; }
    public string[]? SubscribedEventTypes { get; set; }
}


