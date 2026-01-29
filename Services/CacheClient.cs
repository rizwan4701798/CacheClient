using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using CacheClient.Models;
using Newtonsoft.Json;

namespace CacheClient;

public sealed class CacheClient : ICache
{
    private readonly CacheClientOptions _options;
    private bool _initialized;

    private TcpClient? _notificationClient;
    private CancellationTokenSource? _notificationCts;
    private Task? _notificationTask;

    public event EventHandler<CacheEventArgs>? ItemAdded;
    public event EventHandler<CacheEventArgs>? ItemUpdated;
    public event EventHandler<CacheEventArgs>? ItemRemoved;
    public event EventHandler<CacheEventArgs>? ItemExpired;
    public event EventHandler<CacheEventArgs>? ItemEvicted;
    public event EventHandler<CacheEventArgs>? CacheEvent;

    public bool IsSubscribed => _notificationClient?.Connected ?? false;

    public CacheClient(CacheClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public void Initialize()
    {
        _initialized = true;
    }

    public void Add(string key, object? value)
    {
        Add(key, value, null);
    }

    public void Add(string key, object? value, int expirationSeconds)
    {
        Add(key, value, (int?)expirationSeconds);
    }

    private void Add(string key, object? value, int? expirationSeconds)
    {
        var response = Send("CREATE", key, value, expirationSeconds);

        if (!response.Success)
            throw new CacheClientException("Duplicate key.");
    }

    public object? Get(string key)
    {
        var response = Send("READ", key);
        return response.Value;
    }

    public void Update(string key, object? value)
    {
        Update(key, value, null);
    }

    public void Update(string key, object? value, int expirationSeconds)
    {
        Update(key, value, (int?)expirationSeconds);
    }

    private void Update(string key, object? value, int? expirationSeconds)
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

    #region Notification Subscription

    public void Subscribe(params CacheEventType[] eventTypes)
    {
        Subscribe(null, eventTypes);
    }

    public void Subscribe(string? keyPattern, params CacheEventType[] eventTypes)
    {
        ObjectDisposedException.ThrowIf(!_initialized, this);

        if (IsSubscribed)
            throw new InvalidOperationException("Already subscribed. Call Unsubscribe() first.");

        _notificationClient = new TcpClient();
        _notificationClient.Connect(_options.Host, _options.NotificationPort);
        _notificationClient.ReceiveTimeout = 0; 

        var request = new CacheRequest
        {
            Operation = "SUBSCRIBE",
            SubscribedEventTypes = eventTypes.Length > 0
                ? eventTypes.Select(e => e.ToString()).ToArray()
                : null,
            KeyPattern = keyPattern
        };

        var stream = _notificationClient.GetStream();
        var json = JsonConvert.SerializeObject(request);
        var bytes = Encoding.UTF8.GetBytes(json);
        stream.Write(bytes, 0, bytes.Length);

        _notificationCts = new CancellationTokenSource();
        _notificationTask = Task.Run(() => ListenForNotificationsAsync(_notificationCts.Token));
    }

    public void Unsubscribe()
    {
        if (!IsSubscribed) return;

        try
        {
            var request = new CacheRequest { Operation = "UNSUBSCRIBE" };
            var stream = _notificationClient!.GetStream();
            var json = JsonConvert.SerializeObject(request);
            var bytes = Encoding.UTF8.GetBytes(json);
            stream.Write(bytes, 0, bytes.Length);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error during unsubscribe: {ex.Message}");
        }

        _notificationCts?.Cancel();

        try
        {
            _notificationTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException ex)
        {
            Debug.WriteLine($"Error waiting for notification task: {ex.Message}");
        }

        _notificationClient?.Close();
        _notificationClient = null;
        _notificationCts?.Dispose();
        _notificationCts = null;
    }

    private async Task ListenForNotificationsAsync(CancellationToken ct)
    {
        if (_notificationClient is null) return;

        var stream = _notificationClient.GetStream();
        var buffer = new byte[8192];
        var messageBuffer = new StringBuilder();

        while (!ct.IsCancellationRequested && _notificationClient.Connected)
        {
            try
            {
                int bytesRead = await stream.ReadAsync(buffer, ct).ConfigureAwait(false);
                if (bytesRead == 0) break;

                string data = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                messageBuffer.Append(data);

                string content = messageBuffer.ToString();
                int lastNewline = content.LastIndexOf('\n');

                if (lastNewline >= 0)
                {
                    string completeMessages = content[..lastNewline];
                    messageBuffer.Clear();
                    messageBuffer.Append(content[(lastNewline + 1)..]);

                    foreach (var message in completeMessages.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                    {
                        try
                        {
                            var response = JsonConvert.DeserializeObject<CacheResponse>(message);
                            if (response?.IsNotification == true && response.Event is not null)
                            {
                                RaiseEvent(response.Event);
                            }
                        }
                        catch (JsonException ex)
                        {
                            Debug.WriteLine($"Malformed notification message: {ex.Message}");
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Notification listener error: {ex.Message}");
                break;
            }
        }
    }

    private void RaiseEvent(CacheEvent cacheEvent)
    {
        var args = new CacheEventArgs(cacheEvent);

        // Raise specific event
        switch (cacheEvent.EventType)
        {
            case CacheEventType.ItemAdded:
                ItemAdded?.Invoke(this, args);
                break;
            case CacheEventType.ItemUpdated:
                ItemUpdated?.Invoke(this, args);
                break;
            case CacheEventType.ItemRemoved:
                ItemRemoved?.Invoke(this, args);
                break;
            case CacheEventType.ItemExpired:
                ItemExpired?.Invoke(this, args);
                break;
            case CacheEventType.ItemEvicted:
                ItemEvicted?.Invoke(this, args);
                break;
        }

        // Raise catch-all event
        CacheEvent?.Invoke(this, args);
    }

    #endregion

    public void Dispose()
    {
        Unsubscribe();
        _initialized = false;
    }

    private CacheResponse Send(string operation, string? key, object? value = null, int? expirationSeconds = null)
    {
        ObjectDisposedException.ThrowIf(!_initialized, this);

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
        var response = JsonConvert.DeserializeObject<CacheResponse>(responseJson)
            ?? throw new CacheClientException("Invalid response from server");

        if (!string.IsNullOrWhiteSpace(response.Error))
            throw new CacheClientException(response.Error);

        return response;
    }
}
