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
}
