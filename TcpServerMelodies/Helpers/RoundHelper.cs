using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using MelodyLibrary;
using TcpServerMelodies.Models;

namespace TcpServerMelodies.Helpers;

public static class RoundHelper
{
    public static async Task StartRoundAsync(GameSession session)
    {
        int baseLength = 3;

        int mode = session.CurrentRound switch
        {
            <= 6 => 0,   // обычные
            7 or 8 => 1, // на ноту выше
            9 or 10 => 2, // в обратном порядке
            _ => 0
        };

        int length = session.CurrentRound switch
        {
            1 => 3,
            2 => 4,
            3 => 5,
            4 => 3,
            5 => 4,
            6 => 5,
            7 or 8 or 9 or 10 => baseLength,
            _ => 3
        };

        session.RoundMelody = session.CurrentRound == 7 || session.CurrentRound == 8 ? 
            MelodyGenerator.GenerateRandomMelodyFor7And8Round(length) : 
            MelodyGenerator.GenerateRandomMelody(length);
        
        session.Player1AnswerMelody = null;
        session.Player2AnswerMelody = null;

        string notesString = string.Join(",", session.RoundMelody);

        // первые 3 раунда с подсветкой, 7 и 9 раунд без подсветки, остальные c подсветкой
        bool highlight = session.CurrentRound <= 3 || session.CurrentRound == 7 ||  session.CurrentRound == 9;
        string payloadString = $"{session.CurrentRound}|{notesString}|{(highlight ? 1 : 0)}|{mode}";
        
        byte[] payload = Encoding.UTF8.GetBytes(payloadString);
        var packet = new MelodyPackageBuilder(payload, PackageType.RoundStarted).Build();

        await session.Player1!.Connector.SendAsync(packet, SocketFlags.None);
        await session.Player2!.Connector.SendAsync(packet, SocketFlags.None);

        Console.WriteLine($"[Server] ROUND_START {session.JoinCode}: round={session.CurrentRound}, notes={notesString}, highlight={(highlight ? 1 : 0)}");
    }
    
    public static async Task FinishRoundAsync(GameSession session,
        ConcurrentDictionary<string, GameSession> sessions)
    {
        int p1Correct, p2Correct;
        if (session.CurrentRound == 7 || session.CurrentRound == 8)
        {
            p1Correct = CountCorrectNotesFor7And8Rounds(session.RoundMelody, session.Player1AnswerMelody!);
            p2Correct = CountCorrectNotesFor7And8Rounds(session.RoundMelody, session.Player2AnswerMelody!);
        }
        else if (session.CurrentRound == 9 || session.CurrentRound == 10)
        {
            p1Correct = CountCorrectNotesFor9And10Rounds(session.RoundMelody, session.Player1AnswerMelody!);
            p2Correct = CountCorrectNotesFor9And10Rounds(session.RoundMelody, session.Player2AnswerMelody!);
        }
        else
        { // считаем количество правильных нот по позициям
            p1Correct = CountCorrectNotes(session.RoundMelody, session.Player1AnswerMelody!);
            p2Correct = CountCorrectNotes(session.RoundMelody, session.Player2AnswerMelody!);
        }
        
        // 20 баллов за каждую правильную ноту
        int p1Base = p1Correct * 20;
        int p2Base = p2Correct * 20;

        // штраф -5 за каждое дополнительное прослушивание (кроме первого)
        int p1Penalty = Math.Max(0, session.Player1Replays - 1) * 5;
        int p2Penalty = Math.Max(0, session.Player2Replays - 1) * 5;

        int p1Delta = p1Base != 0? p1Base - p1Penalty : 0;
        int p2Delta = p2Base != 0? p2Base - p2Penalty : 0;

        session.Player1!.Score += p1Delta;
        session.Player2!.Score += p2Delta;

        // статус для отображения (OK/FAIL/Partial)
        string p1Status = p1Correct == session.RoundMelody.Count ? "OK" :
                          p1Correct > 0 ? "PARTIAL" : "FAIL";
        string p2Status = p2Correct == session.RoundMelody.Count ? "OK" :
                          p2Correct > 0 ? "PARTIAL" : "FAIL";

        string p1PayloadStr = $"{session.CurrentRound}|{p1Status}|{p1Delta}|{session.Player1.Score}";
        string p2PayloadStr = $"{session.CurrentRound}|{p2Status}|{p2Delta}|{session.Player2.Score}";
        
        Console.WriteLine($"[Server] ROUND_ENDED {session.JoinCode}: round={session.CurrentRound}, p1Status={p1Status}, p1Delta={p1Delta}, p2Status={p2Status}, p2Delta={p2Delta}");

        var p1Packet = new MelodyPackageBuilder(Encoding.UTF8.GetBytes(p1PayloadStr), PackageType.RoundResult).Build();
        var p2Packet = new MelodyPackageBuilder(Encoding.UTF8.GetBytes(p2PayloadStr), PackageType.RoundResult).Build();
        
        await session.Player1.Connector.SendAsync(p1Packet, SocketFlags.None);
        await session.Player2.Connector.SendAsync(p2Packet, SocketFlags.None);


        if (session.CurrentRound == 10)
        {
            Console.WriteLine("[Server] GAME END");
            await FinishGameAsync(session, sessions);
        }
        else
        {
            session.CurrentRound++;
            await StartRoundAsync(session);
        }
    }
    
    public static async Task FinishGameAsync(GameSession session,
    ConcurrentDictionary<string, GameSession> sessions)
    
    {
        var p1 = session.Player1!;
        var p2 = session.Player2!;

        string p1Status;
        string p2Status;

        if (p1.Score > p2.Score)
        {
            p1Status = "WIN";
            p2Status = "LOSE";
        }
        else if (p1.Score < p2.Score)
        {
            p1Status = "LOSE";
            p2Status = "WIN";
        }
        else
        {
            p1Status = "DRAW";
            p2Status = "DRAW";
        }

        string p1PayloadStr = $"{p1Status}|{p1.Score}:{p2.Score}";
        string p2PayloadStr = $"{p2Status}|{p2.Score}:{p1.Score}";

        var p1Packet = new MelodyPackageBuilder(Encoding.UTF8.GetBytes(p1PayloadStr), PackageType.GameResult)
            .Build();

        var p2Packet = new MelodyPackageBuilder(Encoding.UTF8.GetBytes(p2PayloadStr), PackageType.GameResult)
            .Build();

        await p1.Connector.SendAsync(p1Packet, SocketFlags.None);
        await p2.Connector.SendAsync(p2Packet, SocketFlags.None);

        Console.WriteLine($"[Server] GAME_RESULT {session.JoinCode}: " + $"P1={p1PayloadStr}, P2={p2PayloadStr}");
    
        if (p1.SessionCode == session.JoinCode) p1.SessionCode = null;
        if (p2.SessionCode == session.JoinCode) p2.SessionCode = null;

        sessions.TryRemove(session.JoinCode, out _);
    }


    public static int CountCorrectNotes(List<string> correct, List<string> answer)
    {
        int count = Math.Min(correct.Count, answer.Count);
        int correctCount = 0;
        for (int i = 0; i < count; i++)
        {
            if (string.Equals(correct[i], answer[i], StringComparison.OrdinalIgnoreCase))
                correctCount++;
        }
        return correctCount;
    }
    
    // ноты на тон выше
    public static int CountCorrectNotesFor7And8Rounds(List<string> taskNotes, List<string> answer)
    {
        int count = Math.Min(taskNotes.Count, answer.Count);
        int correctCount = 0;
        
        List<string> allNotes =
            ["C", "D", "E", "F", "G", "A", "B"];
        
        int[] correctIndexes =
            [allNotes.IndexOf(taskNotes[0]) + 1, allNotes.IndexOf(taskNotes[1]) + 1, allNotes.IndexOf(taskNotes[2]) + 1];
        
        List<string> correctNotes = [allNotes[correctIndexes[0]], allNotes[correctIndexes[1]], allNotes[correctIndexes[2]]];
        
        for (int i = 0; i < count; i++)
        {
            if (string.Equals(correctNotes[i], answer[i], StringComparison.OrdinalIgnoreCase))
                correctCount++;
        }
        
        return correctCount;
    }
    
    // ноты в обратном порядке
    public static int CountCorrectNotesFor9And10Rounds(List<string> correct, List<string> answer)
    {
        int count = Math.Min(correct.Count, answer.Count);
        int correctCount = 0;

        correct.Reverse();
        
        for (int i = 0; i < count; i++)
        {
            if (string.Equals(correct[i], answer[i], StringComparison.OrdinalIgnoreCase))
                correctCount++;
        }
        
        return correctCount;
    }
}