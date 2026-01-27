namespace CacheClient;

public sealed class CacheClientOptions
{
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 5050;
    public int TimeoutMilliseconds { get; init; } = 5000;
}
