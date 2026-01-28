namespace CacheClient;

/// <summary>
/// Configuration options for the cache client.
/// </summary>
public sealed class CacheClientOptions
{
    /// <summary>
    /// Gets or sets the cache server host address. Default is "localhost".
    /// </summary>
    public string Host { get; init; } = "localhost";

    /// <summary>
    /// Gets or sets the cache server port. Default is 5050.
    /// </summary>
    public int Port { get; init; } = 5050;

    /// <summary>
    /// Gets or sets the notification server port. Default is 5051.
    /// </summary>
    public int NotificationPort { get; init; } = 5051;

    /// <summary>
    /// Gets or sets the timeout in milliseconds for cache operations. Default is 5000ms.
    /// </summary>
    public int TimeoutMilliseconds { get; init; } = 5000;
}
