namespace CacheClient.Models;

public sealed class CacheRequest
{
    public string Operation { get; set; }
    public string Key { get; set; }
    public object Value { get; set; }
    public int? ExpirationSeconds { get; set; }
}

public sealed class CacheResponse
{
    public bool Success { get; set; }
    public object Value { get; set; }
    public string Error { get; set; }
}

