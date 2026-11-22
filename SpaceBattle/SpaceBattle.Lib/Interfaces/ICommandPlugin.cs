using System.Text.Json;

namespace SpaceBattle.Lib;

/// <summary>Provides a named message command for an item authorized by the server.</summary>
public interface ICommandPlugin
{
    string CommandName { get; }
    ICommand Create(IGameItem target, JsonElement arguments);
}
