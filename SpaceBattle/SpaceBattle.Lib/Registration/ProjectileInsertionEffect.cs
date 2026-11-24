using App;

namespace SpaceBattle.Lib;

public sealed class ProjectileInsertionEffect : ITransactionCommandEffect
{
    public string CommandKey => GeneratedNames.CommandDependency(typeof(ShootCommand));

    public ICommand Decorate(object[] args, ICommand command, ITransactionUndo undo) =>
        new ProjectileInsertionCompensation(command, undo,
            Ioc.Resolve<IShooting>(GeneratedNames.AdapterDependency(typeof(IShooting)), args).Storage);
}