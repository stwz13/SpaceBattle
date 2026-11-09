namespace SpaceBattle.Lib;

public sealed class ServerOptions
{
    public int ThreadCount { get; set; } = 2;
    public int InboxCapacity { get; set; } = 1024;
    public int RingReplicas { get; set; } = 64;
    public int QuantumMilliseconds { get; set; } = 10;
    public bool PriorityQueues { get; set; } = true;
    public string[] Games { get; set; } = [];
    public string[] AllowedCommands { get; set; } = [];
    public Dictionary<string, string[]> ItemKinds { get; set; } = [];
    public bool SnapshotOnStop { get; set; }
    public bool RestoreOnStart { get; set; }
    public string SnapshotPath { get; set; } = "data/server-snapshot.json";
    public string JwtIssuer { get; set; } = "SpaceBattle";
    public string JwtAudience { get; set; } = "Players";
    public string JwtSecret { get; set; } = "";
}
