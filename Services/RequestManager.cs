using System.Collections.Concurrent;
using CacheClient.Models;

namespace CacheClient.Services;

public class RequestManager
{
    private readonly ConcurrentQueue<TaskCompletionSource<CacheResponse>> _pendingRequests = new();

    public TaskCompletionSource<CacheResponse> RegisterRequest()
    {
        var tcs = new TaskCompletionSource<CacheResponse>();
        _pendingRequests.Enqueue(tcs);
        return tcs;
    }

    public bool CompleteNext(CacheResponse response)
    {
        if (_pendingRequests.TryDequeue(out var tcs))
        {
            return tcs.TrySetResult(response);
        }
        return false;
    }

    public void CancelAll(Exception exception)
    {
        while (_pendingRequests.TryDequeue(out var tcs))
        {
            tcs.TrySetException(exception);
        }
    }
}
