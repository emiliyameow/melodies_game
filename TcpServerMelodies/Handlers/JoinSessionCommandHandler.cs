using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using MelodyLibrary;
using TcpServerMelodies.Models;

namespace TcpServerMelodies.Handlers;

[Command(PackageType.JoinSession)]
public class JoinSessionCommandHandler : ICommandHandler
{
    public async Task Invoke(Player player, ConcurrentDictionary<string, GameSession> sessions, byte[]? payload = null, CancellationToken ct = default)
    {
        string code = Encoding.UTF8.GetString(payload); // ожидаем "ABCD"

        if (!sessions.TryGetValue(code, out var session))
        {
            var errorBytes = Encoding.UTF8.GetBytes("NOT_FOUND");
            var errorPacket = new MelodyPackageBuilder(errorBytes, PackageType.SessionError);
            await player.Connector.SendAsync(errorPacket.Build(), SocketFlags.None);
            return;
        }

        if (session.Player2 != null)
        {
            var errorBytes = Encoding.UTF8.GetBytes("FULL");
            var errorPacket = new MelodyPackageBuilder(errorBytes, PackageType.SessionError);
            await player.Connector.SendAsync(errorPacket.Build(), SocketFlags.None);
            
            return;
        }

        session.Player2 = player;
        player.SessionCode = code;
        player.IsHost = false;
        player.IsReady = false;
        player.Score = 0;

        Console.WriteLine($"[Server] игрок {player.Connector.RemoteEndPoint} присоединился к сессии {code}");

        // отправляем обоим SESSION_JOINED:<code>
        var payloadBytes = Encoding.UTF8.GetBytes(code);
        var joinedPacket = new MelodyPackageBuilder(payloadBytes, PackageType.SessionJoined).Build();

        await session.Player1!.Connector.SendAsync(joinedPacket, SocketFlags.None);
        await session.Player2!.Connector.SendAsync(joinedPacket, SocketFlags.None);
    }
}