namespace SpaceBattle.Lib;

public interface IRuleCommandFactory<in TTarget>
{
    ICommand Create(TTarget target);
}
