using App;

namespace SpaceBattle.Lib;

/// <summary>Adds ship defaults in a child scope before managed games are registered.</summary>
internal sealed class RegisterIoCDependencyShipDefaults(IReadOnlyDictionary<string, string[]> itemKinds) : ICommand
{
    private readonly IReadOnlyDictionary<string, string[]> _itemKinds =
        itemKinds ?? throw new ArgumentNullException(nameof(itemKinds));

    public void Execute()
    {
        var inner = Ioc.Resolve<IGameItemFactory>(GameItemFactoryDependencies.Factory);
        var factory = new ShipDefaultsGameItemFactory(inner);
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", Ioc.Resolve<object>("IoC.Scope.Create")).Execute();
        IoCRegistrar.Register(GameItemFactoryDependencies.Factory, _ => factory);

        if (_itemKinds.TryGetValue(ShipDefaultsGameItemFactory.ShipKind, out var properties)
            && !properties.Contains(ShipDefaultsGameItemFactory.DestroyedProperty, StringComparer.Ordinal))
        {
            new RegisterIoCDependencyItemKind(ShipDefaultsGameItemFactory.ShipKind,
                properties.Append(ShipDefaultsGameItemFactory.DestroyedProperty)).Execute();
        }
    }
}
