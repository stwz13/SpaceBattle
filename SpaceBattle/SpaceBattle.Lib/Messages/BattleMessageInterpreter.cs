namespace SpaceBattle.Lib;

internal sealed class BattleMessageInterpreter(IMessageInterpreter inner, Func<BattleProgress?> progress) : IMessageInterpreter
{
    public ICommand Interpret(IMessage message)
    {
        if (progress()?.IsFinished == true)
            throw new InvalidOperationException("The battle has already finished.");
        return inner.Interpret(message);
    }
}
