using App;

namespace SpaceBattle.Lib;

public class RegisterIoCDependencyGameItems : ICommand
{
    public void Execute()
    {
        Register(ItemDependencies.Create, args => args is [string kind, string id, IEnumerable<KeyValuePair<string, object>> properties]
            ? Creator().Create(kind, id, properties)
            : throw new ArgumentException("Creating an item requires its kind, its identifier and its properties."));
        Register(MessageDependencies.CommandKey(ItemDependencies.CreateCommandKey), args => args is [IMessage message]
            ? new CreateItemMessageCommand(message, Ioc.Resolve<IValueCodec>(SerializationDependencies.Codec), Creator())
            : throw new ArgumentException("The command requires a message."));
    }

    private static ItemCreator Creator() => new(
        Ioc.Resolve<IGameItemFactory>(GameItemFactoryDependencies.Factory),
        () => Ioc.Resolve<IBattleContext>(BattleContextDependencies.Context).Storage);

    private static void Register(string key, Func<object[], object> strategy) =>
        Ioc.Resolve<App.ICommand>("IoC.Register", key, strategy).Execute();
}
