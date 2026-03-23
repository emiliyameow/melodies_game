using System.Collections.Concurrent;
using TcpServerMelodies.Models;

namespace TcpServerMelodies;

public interface ICommandHandler
{
    Task Invoke(Player player, ConcurrentDictionary<string, GameSession> sessions, byte[]? payload = null,
        CancellationToken ct = default);
}