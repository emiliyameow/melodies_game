using System.Net.Sockets;

namespace TcpServerMelodies.Models;

public class Player
{
    public required Socket Connector { get; set; }
    public bool IsReady { get; set; }
    public string? SessionCode { get; set; }
    public int Score { get; set; }
    public bool IsHost { get; set; }
}