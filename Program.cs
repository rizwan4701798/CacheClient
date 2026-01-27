using System.Net.Sockets;
using System.Text;

class Program
{
    static void Main()
    {
        using var client = new TcpClient("localhost", 5050);
        using var stream = client.GetStream();

        var request = "{\"Operation\":\"CREATE\",\"Key\":\"age\",\"Value\":\"40\"}";
        var data = Encoding.UTF8.GetBytes(request);

        stream.Write(data, 0, data.Length);

        var buffer = new byte[1024];
        int bytes = stream.Read(buffer, 0, buffer.Length);

        var response = Encoding.UTF8.GetString(buffer, 0, bytes);
        Console.WriteLine(response);
    }
}

