using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace CacheClient.Models;

[JsonConverter(typeof(StringEnumConverter))]
public enum CacheOperation
{
    Create,
    Read,
    Update,
    Delete,
    Subscribe,
    Unsubscribe,
    Clear
}
