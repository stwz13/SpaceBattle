namespace SpaceBattle.Lib;

/// <summary>Posts a command for later execution.</summary>
public interface ICommandReceiver
{
    void Receive(ICommand command);
}
