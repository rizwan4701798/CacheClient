namespace CacheClient;

/// <summary>
/// Exception thrown when a cache client operation fails.
/// </summary>
public sealed class CacheClientException : Exception
{
    public CacheClientException()
    {
    }

    public CacheClientException(string message)
        : base(message)
    {
    }

    public CacheClientException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
