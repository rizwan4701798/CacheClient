using System.Text;
using CacheClient.Models;
using Newtonsoft.Json;

namespace CacheClient.Services;

public class CacheSerializer
{
    private readonly JsonSerializer _serializer = new();

    public byte[] Serialize(CacheRequest request)
    {
        var json = JsonConvert.SerializeObject(request);
        return Encoding.UTF8.GetBytes(json);
    }

    public CacheResponse? Deserialize(JsonTextReader reader)
    {
        return _serializer.Deserialize<CacheResponse>(reader);
    }
}
