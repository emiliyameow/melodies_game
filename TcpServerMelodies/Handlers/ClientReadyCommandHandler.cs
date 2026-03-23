using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using MelodyLibrary;
using TcpServerMelodies.Helpers;
using TcpServerMelodies.Models;

namespace TcpServerMelodies.Handlers;

[Command(PackageType.ClientReady)]
public class ClientReadyCommandHandler : ICommandHandler
{
    public async Task Invoke(Player player, ConcurrentDictionary<string, GameSession> sessions, byte[]? payload = null, CancellationToken ct = default)
    {
        if (player.SessionCode == null)
        {
            return;
        }
    
        if (!sessions.TryGetValue(player.SessionCode, out var session))
        {
            return;
        }
    
        player.IsReady = true;
        if (session.Player1.IsReady && session.Player2.IsReady)
        {
            await RoundHelper.StartRoundAsync(session);
        }
        
    }
    
}