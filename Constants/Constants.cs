namespace CacheClient.Constants;

public static class ClientConstants
{
    // Errors & Exceptions
    public const string ConnectionFailed = "Failed to connect to server: {0}";
    public const string UnsupportedOperation = "Unsupported operation for Send: {0}";
    public const string SendRequestFailed = "Failed to send request. Connection might be dropped.";
    public const string RequestTimedOut = "Request timed out waiting for response.";
    public const string ResponseNoPendingRequest = "Received response but no pending request found.";
    public const string ReadLoopError = "Read loop fatal error: {0}";
    public const string ConnectionLost = "Connection lost.";
    public const string UnknownError = "Unknown error";
    public const string KeyDoesNotExist = "Key does not exist.";
    public const string SubscribeTimedOut = "Subscribe timed out";
    public const string UnsubscribeError = "Error during unsubscribe: {0}";
}
