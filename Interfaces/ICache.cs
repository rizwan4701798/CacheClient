namespace CacheClient;

public interface ICache : IDisposable
{
    void Initialize();

    void Add(string key, object value);

    object Get(string key);

    void Update(string key, object value);

    void Remove(string key);

    void Clear();
}
