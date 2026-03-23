namespace TcpServerMelodies.Models;

public class GameSession
{
    public required string JoinCode { get; set; } // код для подключения
    public Player? Player1 { get; set; }
    public Player? Player2 { get; set; }
    public int CurrentRound { get; set; } = 1;
    
    public List<string> RoundMelody { get; set; }
    public List<string> Player1AnswerMelody { get; set; }
    public List<string> Player2AnswerMelody { get; set; }
    
    public int Player1Replays { get; set; }
    public int Player2Replays { get; set; }
}