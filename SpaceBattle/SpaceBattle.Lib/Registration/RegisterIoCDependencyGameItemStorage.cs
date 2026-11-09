using App;

namespace SpaceBattle.Lib;

public class RegisterIoCDependencyGameItemStorage : ICommand
{
    private readonly IGameItemStorage _storage;

    public RegisterIoCDependencyGameItemStorage(IGameItemStorage storage)
    {
        ArgumentNullException.ThrowIfNull(storage);
        _storage = storage;
    }

    public void Execute() => Ioc.Resolve<App.ICommand>("IoC.Register", GameItemStorageDependencies.Storage,
        (object[] args) => _storage).Execute();
}
