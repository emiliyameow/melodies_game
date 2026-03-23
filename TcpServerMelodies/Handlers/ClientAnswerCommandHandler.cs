using System.Collections.Concurrent;
using System.Text;
using MelodyLibrary;
using TcpServerMelodies.Helpers;
using TcpServerMelodies.Models;

namespace TcpServerMelodies.Handlers;

[Command(PackageType.ClientAnswer)]
public class ClientAnswerCommandHandler : ICommandHandler
{
    public async Task Invoke(Player player, ConcurrentDictionary<string, GameSession> sessions, byte[]? payload = null, CancellationToken ct = default)
    {
        if (player.SessionCode == null)
            return;

        if (!sessions.TryGetValue(player.SessionCode, out var session))
            return;
        
        string text = Encoding.UTF8.GetString(payload);   // "1|2|C,E,G"
        var parts = text.Split('|', 3);
        if (parts.Length != 3)
            return;

        if (!int.TryParse(parts[0], out int round))
        {
            return;
        }

        if (round != session.CurrentRound)
        {
            return;
        }

        if (!int.TryParse(parts[1], out int attempts))
        {
            attempts = 1;
        }

        var answerNotes = parts[2]
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        if (player == session.Player1)
        {
            session.Player1AnswerMelody = answerNotes;
            session.Player1Replays = attempts;
        }
        else if (player == session.Player2)
        {
            session.Player2AnswerMelody = answerNotes;
            session.Player2Replays = attempts;
        }

        // когда оба ответили — считаем результат
        if (session.Player1AnswerMelody != null && session.Player2AnswerMelody != null)
        {
            await RoundHelper.FinishRoundAsync(session, sessions);
        }
    }
}