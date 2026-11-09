namespace SpaceBattle.Lib;

public class InterpretMessageCommand : ICommand
{
    private readonly IMessageInterpreter _interpreter;
    private readonly IMessage _message;

    public InterpretMessageCommand(IMessageInterpreter interpreter, IMessage message)
    {
        ArgumentNullException.ThrowIfNull(interpreter);
        ArgumentNullException.ThrowIfNull(message);
        _interpreter = interpreter;
        _message = message;
    }

    public void Execute() => _interpreter.Interpret(_message).Execute();
}
