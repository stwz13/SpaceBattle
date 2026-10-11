namespace SpaceBattle.Lib;

public class SetPropertyCommand : ICommand
{
    private readonly IGameItem _item;
    private readonly string _name;
    private readonly object _value;

    public SetPropertyCommand(IGameItem item, string name, object value)
    {
        _item = item;
        _name = name;
        _value = value;
    }

    public void Execute()
    {
        _item.SetProperty(_name, _value);
    }
}
