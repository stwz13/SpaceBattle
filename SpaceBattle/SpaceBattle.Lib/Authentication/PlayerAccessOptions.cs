using System.Text.Json;
namespace SpaceBattle.Lib;

public sealed class PlayerAccessOptions
{
    public string[] Commands { get; set; } = [];
    public string[] Items { get; set; } = [];
}
