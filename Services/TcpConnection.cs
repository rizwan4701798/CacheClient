using System.Net.Sockets;

namespace CacheClient.Services;

public class TcpConnection : IDisposable
{
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;

    public bool IsConnected => _tcpClient != null && _tcpClient.Connected;
    public NetworkStream? Stream => _stream;

    public void Connect(string host, int port, int sendTimeout)
    {
        _tcpClient = new TcpClient();
        _tcpClient.Connect(host, port);
        _tcpClient.ReceiveTimeout = 0; // Infinite timeout for read loop
        _tcpClient.SendTimeout = sendTimeout;
        
        _stream = _tcpClient.GetStream();
    }

    public void Write(byte[] bytes)
    {
        if (_stream == null) throw new InvalidOperationException("Not connected");
        _stream.Write(bytes, 0, bytes.Length);
        _stream.Flush();
    }

    public void Close()
    {
        try { _tcpClient?.Close(); } catch { }
        _tcpClient = null;
        _stream = null;
    }

    public void Dispose()
    {
        Close();
    }
}
