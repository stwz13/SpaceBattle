namespace SpaceBattle.Lib;

public interface IStrategySourceBuilder
{
    string Build(IEnumerable<Type> interfaces, IEnumerable<(Type Command, Type Interface)> commands);
}
