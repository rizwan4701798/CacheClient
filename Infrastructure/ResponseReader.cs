using System.Diagnostics;
using System.Text;
using CacheClient.Constants;
using CacheClient.Models;
using Newtonsoft.Json;
using CacheClient;

namespace CacheClient.Infrastructure;

public class ResponseReader
{
    private readonly TcpConnection _connection;
    private readonly CacheSerializer _serializer;
    private readonly RequestManager _requestManager;

    public ResponseReader(TcpConnection connection, CacheSerializer serializer, RequestManager requestManager)
    {
        _connection = connection;
        _serializer = serializer;
        _requestManager = requestManager;
    }

    public async Task RunAsync(CancellationToken ct, Action<CacheEvent> onNotificationHandler)
    {
        try
        {
            if (_connection.Stream == null) return;

            using var reader = new StreamReader(_connection.Stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
            using var jsonReader = new JsonTextReader(reader) { CloseInput = false, SupportMultipleContent = true };

            while (!ct.IsCancellationRequested && _connection.IsConnected)
            {
                if (!await jsonReader.ReadAsync(ct).ConfigureAwait(false))
                {
                    break; // End of stream
                }

                var response = _serializer.Deserialize(jsonReader);
                if (response == null) continue;

                if (response.IsNotification && response is NotificationResponse notifResponse && notifResponse.Event != null)
                {
                    onNotificationHandler(notifResponse.Event);
                }
                else
                {
                    if (!_requestManager.CompleteNext(response))
                    {
                        Debug.WriteLine(ClientConstants.ResponseNoPendingRequest);
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Debug.WriteLine(string.Format(ClientConstants.ReadLoopError, ex.Message));
            _requestManager.CancelAll(new CacheClientException(ClientConstants.ConnectionLost, ex));
        }
    }
}
