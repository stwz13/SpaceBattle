using System.Text.Json.Nodes;

namespace SpaceBattle.Lib;

public interface ICommandSerializer
{
    string Tag { get; }
    bool Handles(ICommand command);
    JsonObject Write(ICommand command);
    ICommand Read(JsonObject node);
}
