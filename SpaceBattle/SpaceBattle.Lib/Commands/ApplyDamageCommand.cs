namespace SpaceBattle.Lib;

public class ApplyDamageCommand : ICommand
{
    private readonly IDamageTarget _target;
    private readonly IDamageSource _source;

    public ApplyDamageCommand(IDamageTarget target, IDamageSource source)
    {
        _target = target;
        _source = source;
    }

    public void Execute()
    {
        _target.Health = Math.Max(0, _target.Health - _source.Damage);
    }
}
