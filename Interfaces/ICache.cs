using CacheClient.Models;

namespace CacheClient;

public interface ICache : IDisposable
{

    void Initialize();

    void Add(string key, object? value);

    void Add(string key, object? value, int expirationSeconds);

    object? Get(string key);

    void Update(string key, object? value);

    void Update(string key, object? value, int expirationSeconds);

    void Remove(string key);

    void Clear();

    event EventHandler<CacheEventArgs>? ItemAdded;

    event EventHandler<CacheEventArgs>? ItemUpdated;

    event EventHandler<CacheEventArgs>? ItemRemoved;

    event EventHandler<CacheEventArgs>? ItemExpired;

    event EventHandler<CacheEventArgs>? ItemEvicted;

    event EventHandler<CacheEventArgs>? CacheEvent;

    void Subscribe(params CacheEventType[] eventTypes);

    void Unsubscribe();
    bool IsSubscribed { get; }
}
