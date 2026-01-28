using CacheClient.Models;

namespace CacheClient;

/// <summary>
/// Interface for cache client operations.
/// </summary>
public interface ICache : IDisposable
{
    /// <summary>
    /// Initializes the cache client connection.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Adds a new item to the cache.
    /// </summary>
    /// <param name="key">The unique key for the cache entry.</param>
    /// <param name="value">The value to cache.</param>
    void Add(string key, object? value);

    /// <summary>
    /// Adds a new item to the cache with expiration.
    /// </summary>
    /// <param name="key">The unique key for the cache entry.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="expirationSeconds">Time in seconds until the item expires.</param>
    void Add(string key, object? value, int expirationSeconds);

    /// <summary>
    /// Gets an item from the cache.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <returns>The cached value, or null if not found.</returns>
    object? Get(string key);

    /// <summary>
    /// Updates an existing item in the cache.
    /// </summary>
    /// <param name="key">The key of the entry to update.</param>
    /// <param name="value">The new value.</param>
    void Update(string key, object? value);

    /// <summary>
    /// Updates an existing item in the cache with new expiration.
    /// </summary>
    /// <param name="key">The key of the entry to update.</param>
    /// <param name="value">The new value.</param>
    /// <param name="expirationSeconds">Time in seconds until the item expires.</param>
    void Update(string key, object? value, int expirationSeconds);

    /// <summary>
    /// Removes an item from the cache.
    /// </summary>
    /// <param name="key">The key of the entry to remove.</param>
    void Remove(string key);

    /// <summary>
    /// Clears all items from the cache.
    /// </summary>
    void Clear();

    /// <summary>
    /// Raised when an item is added to the cache.
    /// </summary>
    event EventHandler<CacheEventArgs>? ItemAdded;

    /// <summary>
    /// Raised when an item is updated in the cache.
    /// </summary>
    event EventHandler<CacheEventArgs>? ItemUpdated;

    /// <summary>
    /// Raised when an item is removed from the cache.
    /// </summary>
    event EventHandler<CacheEventArgs>? ItemRemoved;

    /// <summary>
    /// Raised when an item expires in the cache.
    /// </summary>
    event EventHandler<CacheEventArgs>? ItemExpired;

    /// <summary>
    /// Raised when an item is evicted from the cache.
    /// </summary>
    event EventHandler<CacheEventArgs>? ItemEvicted;

    /// <summary>
    /// Raised for any cache event.
    /// </summary>
    event EventHandler<CacheEventArgs>? CacheEvent;

    /// <summary>
    /// Subscribes to cache events.
    /// </summary>
    /// <param name="eventTypes">The event types to subscribe to.</param>
    void Subscribe(params CacheEventType[] eventTypes);

    /// <summary>
    /// Subscribes to cache events with a key pattern filter.
    /// </summary>
    /// <param name="keyPattern">Pattern to filter keys (supports * wildcard at end).</param>
    /// <param name="eventTypes">The event types to subscribe to.</param>
    void Subscribe(string? keyPattern, params CacheEventType[] eventTypes);

    /// <summary>
    /// Unsubscribes from cache events.
    /// </summary>
    void Unsubscribe();

    /// <summary>
    /// Gets a value indicating whether the client is subscribed to events.
    /// </summary>
    bool IsSubscribed { get; }
}
