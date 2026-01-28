using CacheClient.Models;

namespace CacheClient;

public interface ICache : IDisposable
{
    void Initialize();

    void Add(string key, object value);

    void Add(string key, object value, int expirationSeconds);

    object Get(string key);

    void Update(string key, object value);

    void Update(string key, object value, int expirationSeconds);

    void Remove(string key);

    void Clear();

    // Event notifications
    event EventHandler<CacheEventArgs> OnItemAdded;
    event EventHandler<CacheEventArgs> OnItemUpdated;
    event EventHandler<CacheEventArgs> OnItemRemoved;
    event EventHandler<CacheEventArgs> OnItemExpired;
    event EventHandler<CacheEventArgs> OnItemEvicted;
    event EventHandler<CacheEventArgs> OnCacheEvent;

    // Subscription management
    void Subscribe(params CacheEventType[] eventTypes);
    void Subscribe(string keyPattern, params CacheEventType[] eventTypes);
    void Unsubscribe();
    bool IsSubscribed { get; }
}
