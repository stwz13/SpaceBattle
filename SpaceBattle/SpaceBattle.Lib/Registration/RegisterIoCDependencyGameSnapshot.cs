using System.Text.Json.Nodes;
using App;

namespace SpaceBattle.Lib;

public class RegisterIoCDependencyGameSnapshot : ICommand
{
    public void Execute()
    {
        var codec = new JsonValueCodec();
        var serializers = new List<ICommandSerializer> { new MessageCommandSerializer(new IoCMessageInterpreter()) };
        var items = new ItemSerializer(codec, new IoCItemSchema());
        var commands = new CommandJsonSerializer(serializers);
        Register(SerializationDependencies.Codec, args => Checked(args, "The value codec takes no arguments.", codec));
        Register(SerializationDependencies.CodecRegistry, args => Checked(args, "The codec registry takes no arguments.", codec));
        Register(SerializationDependencies.CommandSerializers, args => Checked(args, "The command serializers take no arguments.", serializers));
        new RegisterIoCDependencyValueType<int>("int", value => JsonValue.Create(value), node => node.GetValue<int>()).Execute();
        new RegisterIoCDependencyValueType<long>("long", value => JsonValue.Create(value), node => node.GetValue<long>()).Execute();
        new RegisterIoCDependencyValueType<double>("double", value => JsonValue.Create(value), node => node.GetValue<double>()).Execute();
        new RegisterIoCDependencyValueType<bool>("bool", value => JsonValue.Create(value), node => node.GetValue<bool>()).Execute();
        new RegisterIoCDependencyValueType<string>("string", value => JsonValue.Create(value)!, node => node.GetValue<string>()).Execute();
        new RegisterIoCDependencyValueType<Angle>("angle", value => JsonValue.Create(value.Numerator),
            node => new Angle(node.GetValue<int>())).Execute();
        new RegisterIoCDependencyValueType<Vector>("vector",
            value => new JsonArray(value.Coordinates.Select(coordinate => (JsonNode?)JsonValue.Create(coordinate)).ToArray()),
            node => new Vector(node.AsArray().Select(coordinate => coordinate!.GetValue<int>()).ToArray())).Execute();
        Register(SerializationDependencies.Snapshot, args => args is [IGame game, Action<string> sink]
            ? new SnapshotGameCommand(game, Storage, items, commands, sink)
            : throw new ArgumentException("A snapshot requires a game and a receiver of the result."));
        Register(SerializationDependencies.Restore, args =>
        {
            if (args is not [string json])
            {
                throw new ArgumentException("A restore requires the serialized game.");
            }
            var snapshot = JsonNode.Parse(json)!.AsObject();
            var game = Ioc.Resolve<IGame>(GameDependencies.Create, snapshot["gameId"]!.GetValue<string>());
            game.Post(new RestoreItemsCommand(snapshot["items"]!.AsArray(), Storage, items,
                Ioc.Resolve<IGameItemFactory>(GameItemFactoryDependencies.Factory)));
            snapshot["commands"]!.AsArray().Select(node => node!.AsObject()).ToList()
                .ForEach(node => game.Post(commands.Read(node)));
            return game;
        });
    }

    private static object Checked(object[] args, string message, object result) =>
        args.Length == 0 ? result : throw new ArgumentException(message);

    private static IGameItemStorage Storage() =>
        Ioc.Resolve<IBattleContext>(BattleContextDependencies.Context).Storage;

    private static void Register(string key, Func<object[], object> strategy) =>
        Ioc.Resolve<App.ICommand>("IoC.Register", key, strategy).Execute();
}
