using App;

namespace SpaceBattle.Lib;

public class RegisterIoCDependencyCollisions(string directory) : ICommand
{
    public const string Extension = "*.collisions";
    public const string Prefix = "Collisions";
    public const string CommandKey = "Commands.Collision";
    public const string PositionProperty = "Position";
    public const string VelocityProperty = "Velocity";

    public void Execute()
    {
        foreach (var path in Directory.EnumerateFiles(directory, Extension, SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.Ordinal))
        {
            var tree = new Lazy<CollisionTree>(() => new CollisionTree(CollisionFile.Parse(File.ReadLines(path))));
            Register(Key(path), args => tree.Value);
        }

        Register(CommandKey, args => new CollisionCommand(
            Ioc.Resolve<CollisionTree>((string)args[0]),
            () => Offset((IGameItem)args[1], (IGameItem)args[2]),
            (ICommand)args[3]));
    }

    private string Key(string path) => Prefix + "." + string.Join('.',
        Path.GetRelativePath(directory, Path.ChangeExtension(path, null))
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

    private static Vector Offset(IGameItem first, IGameItem second) => new Vector(
        Difference(first, second, PositionProperty).Concat(Difference(first, second, VelocityProperty)).ToArray());

    private static IEnumerable<int> Difference(IGameItem first, IGameItem second, string property) =>
        ((Vector)second.GetProperty(property)).Coordinates
            .Zip(((Vector)first.GetProperty(property)).Coordinates, (b, a) => b - a);

    private static void Register(string name, Func<object[], object> strategy)
    {
        Ioc.Resolve<App.ICommand>("IoC.Register", name, strategy).Execute();
    }
}
