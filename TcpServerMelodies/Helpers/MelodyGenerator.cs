namespace TcpServerMelodies.Helpers;

public static class MelodyGenerator
{
    private static readonly List<string> AllNotes = ["C", "D", "E", "F", "G", "A", "B"];
    private static readonly Random Random = new();

    public static List<string> GenerateRandomMelody(int length)
    {
        var result = new List<string>(length);
        for (int i = 0; i < length; i++)
        {
            result.Add(AllNotes[Random.Next(0, AllNotes.Count)]);
        }

        return result;
    }
    
    public static List<string> GenerateRandomMelodyFor7And8Round(int length)
    {
        var result = new List<string>(length);
        for (int i = 0; i < length; i++)
        {
            result.Add(AllNotes[Random.Next(0, AllNotes.Count - 1)]);
        }

        return result;
    }
}