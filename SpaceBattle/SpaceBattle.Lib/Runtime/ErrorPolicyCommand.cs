namespace SpaceBattle.Lib;

public sealed class ErrorPolicyCommand(Action execute, IErrorRecovery recovery) : ICommand
{
    public void Execute() => recovery.Execute(execute);
}
