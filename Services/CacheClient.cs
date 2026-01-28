using System.Net.Sockets;
using System.Text;
using CacheClient.Models;
using Newtonsoft.Json;

namespace CacheClient;

public sealed class CacheClient : ICache
{
    private readonly CacheClientOptions _options;
    private bool _initialized;

    public CacheClient(CacheClientOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public void Initialize()
    {
        // No persistent connection required (thin client)
        _initialized = true;
    }

    public void Add(string key, object value)
    {
        Add(key, value, null);
    }

    public void Add(string key, object value, int expirationSeconds)
    {
        Add(key, value, (int?)expirationSeconds);
    }

    private void Add(string key, object value, int? expirationSeconds)
    {
        var response = Send("CREATE", key, value, expirationSeconds);

        if (!response.Success)
            throw new CacheClientException("Duplicate key.");
    }

    public object Get(string key)
    {
        var response = Send("READ", key);

        return response.Value;
    }

    public void Update(string key, object value)
    {
        Update(key, value, null);
    }

    public void Update(string key, object value, int expirationSeconds)
    {
        Update(key, value, (int?)expirationSeconds);
    }

    private void Update(string key, object value, int? expirationSeconds)
    {
        var response = Send("UPDATE", key, value, expirationSeconds);

        if (!response.Success)
            throw new CacheClientException("Key does not exist.");
    }

    public void Remove(string key)
    {
        Send("DELETE", key);
    }

    public void Clear()
    {
        Send("CLEAR", null);
    }

    public void Dispose()
    {
        // Stateless client → nothing to dispose
        _initialized = false;
    }

    // -------------------------
    // Internal Driver Method
    // -------------------------
    private CacheResponse Send(string operation, string key, object value = null, int? expirationSeconds = null)
    {
        if (!_initialized)
            throw new InvalidOperationException("Cache client not initialized.");

        var request = new CacheRequest
        {
            Operation = operation,
            Key = key,
            Value = value,
            ExpirationSeconds = expirationSeconds
        };

        using var client = new TcpClient();
        client.Connect(_options.Host, _options.Port);
        client.ReceiveTimeout = _options.TimeoutMilliseconds;
        client.SendTimeout = _options.TimeoutMilliseconds;

        using var stream = client.GetStream();

        var json = JsonConvert.SerializeObject(request);
        var bytes = Encoding.UTF8.GetBytes(json);
        stream.Write(bytes, 0, bytes.Length);

        var buffer = new byte[4096];
        int read = stream.Read(buffer, 0, buffer.Length);

        var responseJson = Encoding.UTF8.GetString(buffer, 0, read);
        var response = JsonConvert.DeserializeObject<CacheResponse>(responseJson);

        if (!string.IsNullOrWhiteSpace(response.Error))
            throw new CacheClientException(response.Error);

        return response;
    }
}
