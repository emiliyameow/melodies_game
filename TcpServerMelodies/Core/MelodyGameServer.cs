using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using MelodyLibrary;
using TcpServerMelodies.Models;

namespace TcpServerMelodies;

public class MelodyGameServer : IDisposable
{
    private readonly Socket _server;
    
    private readonly ConcurrentDictionary<string, GameSession> _sessionsByCode = new();
    private readonly ConcurrentDictionary<string, Player> _players = new();
    
    public MelodyGameServer(IPEndPoint  endPoint)
    {
        _server = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        _server.Bind(endPoint);
        _server.Listen(8);
    }
    
    public async Task StartServer()
    {
        var cts = new CancellationTokenSource();

        Console.WriteLine("[Server] Запущен. Ждем подключений...");

        try
        {
            while (!cts.IsCancellationRequested)
            {
                var connection = await _server.AcceptAsync(cts.Token);
                
                _ = Task.Run(() => HandleConnection(connection, cts), cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("[Server] Остановлен по запросу.");
        }
    }

    
    private async Task HandleConnection(Socket playerSocket, CancellationTokenSource cts)
    {
        string playerId = playerSocket.RemoteEndPoint?.ToString() ?? "Unknown";

        var player = new Player
        {
            Connector = playerSocket,
            IsReady = false,
            SessionCode = null,
            Score = 0,
            IsHost = false
        };

        _players[playerId] = player;
        Console.WriteLine($"[+] {playerId} подключен.");

        var reader = new MelodyPackageReader(playerSocket);

        try
        {
            while (true)
            {
                var packet = await reader.ReadNextAsync(cts.Token);
                if (packet is null)
                {
                    break; // клиент отключился
                }
                
                var handler = CommandHandlerFactory.GetHandler(packet.Type);
                if (handler is null)
                {
                    continue;
                }

                await handler.Invoke(player, _sessionsByCode, packet.Payload, cts.Token);
                
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[!] ошибка клиента {playerId}: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
        finally
        {
            _players.TryRemove(playerId, out _);
            playerSocket.Dispose();
            Console.WriteLine($"[-] {playerId} отключен.");
        }
    }
    
    public void Dispose()
    {
        _server.Dispose();
        
        // Закрываем сокеты всех клиентов
        foreach (var player in _players.Values)
        {
            player.Connector.Dispose();
        }
        _players.Clear();
    }
}