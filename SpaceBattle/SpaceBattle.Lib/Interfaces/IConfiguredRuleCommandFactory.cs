namespace SpaceBattle.Lib;

public interface IConfiguredRuleCommandFactory<in TTarget, in TSettings>
{
    ICommand Create(TTarget target, TSettings settings);
}
