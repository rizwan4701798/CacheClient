using System.Net.Sockets;
using System.Text;
using CacheClient.Models;
using Newtonsoft.Json;

namespace CacheClient;

public sealed class CacheClient : ICache
{
    private readonly CacheClientOptions _options;
    private bool _initialized;

    // Notification support
    private TcpClient _notificationClient;
    private CancellationTokenSource _notificationCts;
    private Task _notificationTask;

    // Events
    public event EventHandler<CacheEventArgs> OnItemAdded;
    public event EventHandler<CacheEventArgs> OnItemUpdated;
    public event EventHandler<CacheEventArgs> OnItemRemoved;
    public event EventHandler<CacheEventArgs> OnItemExpired;
    public event EventHandler<CacheEventArgs> OnItemEvicted;
    public event EventHandler<CacheEventArgs> OnCacheEvent;

    public bool IsSubscribed => _notificationClient?.Connected ?? false;

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

    #region Notification Subscription

    public void Subscribe(params CacheEventType[] eventTypes)
    {
        Subscribe(null, eventTypes);
    }

    public void Subscribe(string keyPattern, params CacheEventType[] eventTypes)
    {
        if (!_initialized)
            throw new InvalidOperationException("Cache client not initialized.");

        if (IsSubscribed)
            throw new InvalidOperationException("Already subscribed. Call Unsubscribe() first.");

        _notificationClient = new TcpClient();
        _notificationClient.Connect(_options.Host, _options.NotificationPort);
        _notificationClient.ReceiveTimeout = 0; // No timeout for notification stream

        // Send subscription request
        var request = new CacheRequest
        {
            Operation = "SUBSCRIBE",
            SubscribedEventTypes = eventTypes.Length > 0
                ? eventTypes.Select(e => e.ToString()).ToArray()
                : null, // null means all events
            KeyPattern = keyPattern
        };

        var stream = _notificationClient.GetStream();
        var json = JsonConvert.SerializeObject(request);
        var bytes = Encoding.UTF8.GetBytes(json);
        stream.Write(bytes, 0, bytes.Length);

        // Start listening for notifications
        _notificationCts = new CancellationTokenSource();
        _notificationTask = Task.Run(() => ListenForNotificationsAsync(_notificationCts.Token));
    }

    public void Unsubscribe()
    {
        if (!IsSubscribed) return;

        try
        {
            // Send unsubscribe request
            var request = new CacheRequest { Operation = "UNSUBSCRIBE" };
            var stream = _notificationClient.GetStream();
            var json = JsonConvert.SerializeObject(request);
            var bytes = Encoding.UTF8.GetBytes(json);
            stream.Write(bytes, 0, bytes.Length);
        }
        catch
        {
            // Ignore errors during unsubscribe
        }

        _notificationCts?.Cancel();

        try
        {
            _notificationTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Ignore timeout
        }

        _notificationClient?.Close();
        _notificationClient = null;
        _notificationCts?.Dispose();
        _notificationCts = null;
    }

    private async Task ListenForNotificationsAsync(CancellationToken ct)
    {
        var stream = _notificationClient.GetStream();
        var buffer = new byte[8192];
        var messageBuffer = new StringBuilder();

        while (!ct.IsCancellationRequested && _notificationClient.Connected)
        {
            try
            {
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct);
                if (bytesRead == 0) break;

                string data = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                messageBuffer.Append(data);

                // Process complete messages (delimited by newline)
                string content = messageBuffer.ToString();
                int lastNewline = content.LastIndexOf('\n');

                if (lastNewline >= 0)
                {
                    string completeMessages = content.Substring(0, lastNewline);
                    messageBuffer.Clear();
                    messageBuffer.Append(content.Substring(lastNewline + 1));

                    foreach (var message in completeMessages.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                    {
                        try
                        {
                            var response = JsonConvert.DeserializeObject<CacheResponse>(message);
                            if (response?.IsNotification == true && response.Event != null)
                            {
                                RaiseEvent(response.Event);
                            }
                        }
                        catch
                        {
                            // Skip malformed messages
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Connection lost or other error
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
                OnItemAdded?.Invoke(this, args);
                break;
            case CacheEventType.ItemUpdated:
                OnItemUpdated?.Invoke(this, args);
                break;
            case CacheEventType.ItemRemoved:
                OnItemRemoved?.Invoke(this, args);
                break;
            case CacheEventType.ItemExpired:
                OnItemExpired?.Invoke(this, args);
                break;
            case CacheEventType.ItemEvicted:
                OnItemEvicted?.Invoke(this, args);
                break;
        }

        // Raise catch-all event
        OnCacheEvent?.Invoke(this, args);
    }

    #endregion

    public void Dispose()
    {
        Unsubscribe();
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
