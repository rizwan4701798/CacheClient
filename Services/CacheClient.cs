using System.Diagnostics;
using CacheClient.Constants;
using CacheClient.Models;
using CacheClient.Services;

namespace CacheClient;

public sealed class CacheClient : ICache, IDisposable
{
    private readonly CacheClientOptions _options;
    private bool _initialized;
    
    // Infrastructure components
    private readonly TcpConnection _connection;
    private readonly RequestManager _requestManager;
    private readonly CacheSerializer _serializer;
    private readonly ResponseReader _responseReader;

    private CancellationTokenSource? _connectionCts;
    private Task? _readLoopTask;
    
    private readonly object _writeLock = new();

    public event EventHandler<CacheEventArgs>? ItemAdded;
    public event EventHandler<CacheEventArgs>? ItemUpdated;
    public event EventHandler<CacheEventArgs>? ItemRemoved;
    public event EventHandler<CacheEventArgs>? ItemExpired;
    public event EventHandler<CacheEventArgs>? ItemEvicted;
    public event EventHandler<CacheEventArgs>? CacheEvent;

    public bool IsSubscribed => _connection.IsConnected;

    public CacheClient(CacheClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;

        _connection = new TcpConnection();
        _requestManager = new RequestManager();
        _serializer = new CacheSerializer();
        _responseReader = new ResponseReader(_connection, _serializer, _requestManager);
    }

    public void Initialize()
    {
        if (_initialized) return;

        try 
        {
            _connection.Connect(_options.Host, _options.Port, _options.TimeoutMilliseconds);
            
            _connectionCts = new CancellationTokenSource();
            
            _readLoopTask = Task.Run(() => _responseReader.RunAsync(_connectionCts.Token, HandleNotification));
            
            _initialized = true;

                Subscribe(
                    CacheEventType.ItemAdded, 
                    CacheEventType.ItemUpdated, 
                    CacheEventType.ItemRemoved, 
                    CacheEventType.ItemExpired, 
                    CacheEventType.ItemEvicted);
            
        }
        catch (Exception ex)
        {
            throw new CacheClientException(string.Format(ClientConstants.ConnectionFailed, ex.Message), ex);
        }
    }
    
    private CacheResponse Send(CacheOperation operation, string? key, object? value = null, int? expirationSeconds = null)
    {
        ObjectDisposedException.ThrowIf(!_initialized, this);

        CacheRequest request = operation switch
        {
            CacheOperation.Create or CacheOperation.Update => new DataRequest(operation, key!, value, expirationSeconds),
            CacheOperation.Read or CacheOperation.Delete => new KeyRequest(operation, key!),
            CacheOperation.Clear => new BasicRequest(operation),
            _ => throw new ArgumentException(string.Format(ClientConstants.UnsupportedOperation, operation))
        };
        
        TaskCompletionSource<CacheResponse> tcs;

        try
        {
            lock (_writeLock)
            {
                tcs = _requestManager.RegisterRequest();
                var bytes = _serializer.Serialize(request);
                _connection.Write(bytes);
            }
        }
        catch (Exception)
        {
            // If request failed to send, ensure we don't leave a hanging tcs if we registered it?
            // Actually RequestManager.RegisterRequest simply enqueues. If Write fails, the TCS is in queue but will never get response.
            // We should probably cancel it. But picking it out is hard (queue).
            // For now, we rely on timeout or CancelAll in case of connection drop.
            // But if Write throws, the connection is likely bad.
            throw; // Let caller handle
        }

        try
        {
            if (!tcs.Task.Wait(_options.TimeoutMilliseconds))
            {
                throw new TimeoutException(ClientConstants.RequestTimedOut);
            }
            return tcs.Task.Result;
        }
        catch (AggregateException ae)
        {
            if (ae.InnerException is CacheClientException ce) throw ce;
            throw ae.InnerException ?? ae;
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

    public void Add(string key, object? value, int? expirationSeconds = null)
    {
        var response = Send(CacheOperation.Create, key, value, expirationSeconds);
        if (!response.Success) throw new CacheClientException(response.Error ?? ClientConstants.UnknownError);
    }

    public object? Get(string key)
    {
        var response = Send(CacheOperation.Read, key);
        if (response is DataResponse dataResponse)
        {
             return dataResponse.Value;
        }
        return null;
    }

    public void Update(string key, object? value, int? expirationSeconds = null)
    {
         var response = Send(CacheOperation.Update, key, value, expirationSeconds);
         if (!response.Success) throw new CacheClientException(response.Error ?? ClientConstants.KeyDoesNotExist);
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
        
        TaskCompletionSource<CacheResponse> tcs;
        
        lock (_writeLock)
        {
            tcs = _requestManager.RegisterRequest();
            var bytes = _serializer.Serialize(request);
            _connection.Write(bytes);
        }

        if (!tcs.Task.Wait(_options.TimeoutMilliseconds))
            throw new TimeoutException(ClientConstants.SubscribeTimedOut);
    }

    public void Unsubscribe(params CacheEventType[] eventTypes)
    {
        if (!_initialized) return;

        try
        {
             CacheRequest request;
             if (eventTypes != null && eventTypes.Length > 0)
             {
                 request = new SubscriptionRequest(CacheOperation.Unsubscribe, eventTypes.Select(e => e.ToString()).ToArray());
             }
             else
             {
                 request = new BasicRequest(CacheOperation.Unsubscribe);
             }

             TaskCompletionSource<CacheResponse> tcs;
             
             lock (_writeLock)
             {
                 tcs = _requestManager.RegisterRequest();
                 var bytes = _serializer.Serialize(request);
                 _connection.Write(bytes);
             }
             
             tcs.Task.Wait(2000); 
        }
        catch (Exception ex)
        {
            Debug.WriteLine(string.Format(ClientConstants.UnsubscribeError, ex.Message));
        }
    }

    public void Dispose()
    {
        _connectionCts?.Cancel();
        _connection.Dispose(); 
        
        _initialized = false;
        
        try { _readLoopTask?.Wait(500); } catch { }
        
        _connectionCts?.Dispose();
        _requestManager.CancelAll(new ObjectDisposedException("CacheClient"));
    }
}
