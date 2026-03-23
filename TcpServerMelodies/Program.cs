using System.Net;
using System.Net.Sockets;

namespace TcpServerMelodies;

class Program
{
    public static async  Task Main(string[] args)
    {
        using var server = new MelodyGameServer(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8888));
        
        await server.StartServer();
    }
}



