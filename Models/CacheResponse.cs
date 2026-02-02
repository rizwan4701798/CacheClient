using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CacheClient.Models;

[JsonConverter(typeof(CacheResponseConverter))]
public class CacheResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public bool IsNotification { get; set; }
}

public class SuccessResponse : CacheResponse
{
    public SuccessResponse()
    {
        Success = true;
    }
}

public class DataResponse : SuccessResponse
{
    public object? Value { get; set; }

    public DataResponse(object? value)
    {
        Value = value;
    }
}

public class ErrorResponse : CacheResponse
{
    public ErrorResponse(string error)
    {
        Success = false;
        Error = error;
    }
}

public class NotificationResponse : CacheResponse
{
    public CacheEvent? Event { get; set; }

    public NotificationResponse(CacheEvent cacheEvent)
    {
        IsNotification = true;
        Event = cacheEvent;
        Success = true;
    }
}

public class CacheResponseConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return typeof(CacheResponse).IsAssignableFrom(objectType);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        var jsonObject = JObject.Load(reader);

        CacheResponse response;

        if (jsonObject["IsNotification"]?.Value<bool>() == true)
        {
            var eventData = jsonObject["Event"]?.ToObject<CacheEvent>(serializer);
            response = new NotificationResponse(eventData!);
        }
        else if (jsonObject.ContainsKey("Error") && jsonObject["Error"]!.Type != JTokenType.Null)
        {
            var error = jsonObject["Error"]!.Value<string>();
            response = new ErrorResponse(error!);
        }
        else if (jsonObject.ContainsKey("Value"))
        {
            var value = jsonObject["Value"]?.ToObject<object>(serializer);
            response = new DataResponse(value);
        }
        else
        {
            // Default to SuccessResponse or base CacheResponse if just success=true/false
            // If Success is false but no Error?? 
            // Stick to base or SuccessResponse.
            response = new SuccessResponse(); 
        }

        serializer.Populate(jsonObject.CreateReader(), response);
        return response;
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        throw new NotImplementedException("Client only reads responses.");
    }
    
    public override bool CanWrite => false;
}
