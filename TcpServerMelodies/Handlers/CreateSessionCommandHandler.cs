using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using MelodyLibrary;
using TcpServerMelodies.Models;

namespace TcpServerMelodies.Handlers;

[Command(PackageType.CreateSession)]
public class CreateSessionCommandHandler : ICommandHandler
{
    public async Task Invoke(Player player, ConcurrentDictionary<string, GameSession> sessions, 
        byte[]? payload = null, CancellationToken ct = default)
    {
        if (player.SessionCode != null)
        {
            var errorBytes = Encoding.UTF8.GetBytes("ALREADY_IN_SESSION");
            var errorPacket = new MelodyPackageBuilder(errorBytes, PackageType.SessionError);
            await player.Connector.SendAsync(errorPacket.Build(), SocketFlags.None);
            return;
        }

        // генерируем уникальный код
        string code;
        do
        {
            code = GenerateSessionCode();
        } while (sessions.ContainsKey(code));

        // создаем сессию
        var session = new GameSession
        {
            JoinCode = code,
            Player1 = player,
            Player2 = null,
            CurrentRound = 1,
            RoundMelody = new List<string>(),
            Player1AnswerMelody = new List<string>(),
            Player2AnswerMelody = new List<string>()
        };

        sessions[code] = session;

        // помечаем игрока как хоста
        player.SessionCode = code;
        player.IsHost = true;
        player.IsReady = false;
        player.Score = 0;

        Console.WriteLine($"[Server] создана сессия {code} для игрока {player.Connector.RemoteEndPoint}");

        // отправляем клиенту SESSION_CREATED:<code>
        var payloadNew = Encoding.UTF8.GetBytes(code);
        var packetBuilder = new MelodyPackageBuilder(payloadNew, PackageType.SessionCreated);
        var packet = packetBuilder.Build();

        await player.Connector.SendAsync(packet, SocketFlags.None);
    }

    private static string GenerateSessionCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var rnd = new Random();
        char[] code = new char[4];
        for (int i = 0; i < code.Length; i++)
        {
            code[i] = chars[rnd.Next(chars.Length)];
        }
        return new string(code);
    }
}
