using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using CacheClient.Models;
using Newtonsoft.Json;

namespace CacheClient;

public sealed class CacheClient : ICache, IDisposable
{
    private readonly CacheClientOptions _options;
    private bool _initialized;
    
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private CancellationTokenSource? _connectionCts;
    private Task? _readLoopTask;
    
    // Queue for pending requests to match responses
    private readonly ConcurrentQueue<TaskCompletionSource<CacheResponse>> _pendingRequests = new();
    private readonly object _writeLock = new();

    public event EventHandler<CacheEventArgs>? ItemAdded;
    public event EventHandler<CacheEventArgs>? ItemUpdated;
    public event EventHandler<CacheEventArgs>? ItemRemoved;
    public event EventHandler<CacheEventArgs>? ItemExpired;
    public event EventHandler<CacheEventArgs>? ItemEvicted;
    public event EventHandler<CacheEventArgs>? CacheEvent;

    public bool IsSubscribed => _tcpClient != null && _tcpClient.Connected;

    public CacheClient(CacheClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public void Initialize()
    {
        if (_initialized) return;

        try 
        {
            _tcpClient = new TcpClient();
            _tcpClient.Connect(_options.Host, _options.Port);
            _tcpClient.ReceiveTimeout = 0; // Infinite timeout for the read loop
            _tcpClient.SendTimeout = _options.TimeoutMilliseconds;
            
            _stream = _tcpClient.GetStream();
            _connectionCts = new CancellationTokenSource();
            
            _readLoopTask = Task.Run(() => ReadLoopAsync(_connectionCts.Token));
            
            _initialized = true;
        }
        catch (Exception ex)
        {
            throw new CacheClientException($"Failed to connect to server: {ex.Message}", ex);
        }
    }
    
    // Generic Send method using TaskCompletionSource for response matching
    private CacheResponse Send(CacheOperation operation, string? key, object? value = null, int? expirationSeconds = null)
    {
        ObjectDisposedException.ThrowIf(!_initialized, this);

        CacheRequest request = operation switch
        {
            CacheOperation.Create or CacheOperation.Update => new DataRequest(operation, key!, value, expirationSeconds),
            CacheOperation.Read or CacheOperation.Delete => new KeyRequest(operation, key!),
            CacheOperation.Clear => new BasicRequest(operation),
            _ => throw new ArgumentException($"Unsupported operation for Send: {operation}")
        };
        
        var tcs = new TaskCompletionSource<CacheResponse>();

        try
        {
            lock (_writeLock)
            {
                _pendingRequests.Enqueue(tcs);
                var json = JsonConvert.SerializeObject(request);
                var bytes = Encoding.UTF8.GetBytes(json);
                _stream!.Write(bytes, 0, bytes.Length);
                _stream.Flush();
            }
        }
        catch (Exception)
        {
            // If write fails, remove TCS and throw
            // Since ConcurrentQueue doesn't support removal from middle easily, we will fail the specific request if possible
            // But usually this means connection dead.
            // We set exception on TCS to unblock manual waiter if generic.
            tcs.TrySetException(new CacheClientException("Failed to send request. Connection might be dropped."));
            throw;
        }

        // Wait for response synchronously to match interface
        try
        {
            if (!tcs.Task.Wait(_options.TimeoutMilliseconds))
            {
                throw new TimeoutException("Request timed out waiting for response.");
            }
            return tcs.Task.Result;
        }
        catch (AggregateException ae)
        {
            if (ae.InnerException is CacheClientException ce) throw ce;
            throw ae.InnerException ?? ae;
        }
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        try
        {
            using var reader = new StreamReader(_stream!, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
            using var jsonReader = new JsonTextReader(reader) { CloseInput = false, SupportMultipleContent = true };
            var serializer = new JsonSerializer();

            while (!ct.IsCancellationRequested && _tcpClient != null && _tcpClient.Connected)
            {
                if (!await jsonReader.ReadAsync(ct).ConfigureAwait(false))
                {
                    break; // End of stream
                }

                var response = serializer.Deserialize<CacheResponse>(jsonReader);
                if (response == null) continue;

                if (response.IsNotification && response.Event != null)
                {
                    // Handle Notification
                    HandleNotification(response.Event);
                }
                else
                {
                    // Handle Request Response
                    if (_pendingRequests.TryDequeue(out var tcs))
                    {
                        if (response.Success)
                        {
                            tcs.TrySetResult(response);
                        }
                        else
                        {
                            // If server sent error, return it as result so caller can see error message
                            // or throw? The interface expects Check Success.
                            tcs.TrySetResult(response); 
                        }
                    }
                    else
                    {
                         Debug.WriteLine("Received response but no pending request found.");
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Debug.WriteLine($"Read loop fatal error: {ex.Message}");
            // Fail all pending requests
            while (_pendingRequests.TryDequeue(out var tcs))
            {
                tcs.TrySetException(new CacheClientException("Connection lost.", ex));
            }
        }
        finally
        {
            _initialized = false;
        }
    }

    private void HandleNotification(CacheEvent cacheEvent)
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

    public void Add(string key, object? value) => Add(key, value, null);

    public void Add(string key, object? value, int expirationSeconds) => Add(key, value, (int?)expirationSeconds);

    private void Add(string key, object? value, int? expirationSeconds)
    {
        var response = Send(CacheOperation.Create, key, value, expirationSeconds);
        if (!response.Success) throw new CacheClientException(response.Error ?? "Unknown error");
    }

    public object? Get(string key)
    {
        var response = Send(CacheOperation.Read, key);
        return response.Value;
    }

    public void Update(string key, object? value) => Update(key, value, null);

    public void Update(string key, object? value, int expirationSeconds) => Update(key, value, (int?)expirationSeconds);

    private void Update(string key, object? value, int? expirationSeconds)
    {
         var response = Send(CacheOperation.Update, key, value, expirationSeconds);
         if (!response.Success) throw new CacheClientException(response.Error ?? "Key does not exist.");
    }

    public void Remove(string key)
    {
        Send(CacheOperation.Delete, key);
    }

    public void Clear()
    {
        Send(CacheOperation.Clear, null);
    }

    public void Subscribe(params CacheEventType[] eventTypes)
    {
        ObjectDisposedException.ThrowIf(!_initialized, this);
        
        var request = new SubscriptionRequest(eventTypes.Select(e => e.ToString()).ToArray());
        
        var tcs = new TaskCompletionSource<CacheResponse>();
        
        lock (_writeLock)
        {
            _pendingRequests.Enqueue(tcs);
            var json = JsonConvert.SerializeObject(request);
            var bytes = Encoding.UTF8.GetBytes(json);
            _stream!.Write(bytes, 0, bytes.Length);
            _stream.Flush();
        }

        // Wait for Ack
        if (!tcs.Task.Wait(_options.TimeoutMilliseconds))
            throw new TimeoutException("Subscribe timed out");
    }

    public void Unsubscribe()
    {
        if (!_initialized) return;

        try
        {
             var request = new BasicRequest(CacheOperation.Unsubscribe);
             var tcs = new TaskCompletionSource<CacheResponse>();
             
             lock (_writeLock)
             {
                _pendingRequests.Enqueue(tcs);
                var json = JsonConvert.SerializeObject(request);
                var bytes = Encoding.UTF8.GetBytes(json);
                _stream!.Write(bytes, 0, bytes.Length);
                _stream.Flush();
             }
             
             tcs.Task.Wait(2000); // Wait briefly for ack
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error during unsubscribe: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _connectionCts?.Cancel();
        _tcpClient?.Close();
        _initialized = false;
        
        // Wait for loop to finish?
        try { _readLoopTask?.Wait(500); } catch { }
        
        _connectionCts?.Dispose();
    }
}
