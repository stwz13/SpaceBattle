using App;

namespace SpaceBattle.Lib;

public class RegisterIoCDependencyCollisions(string directory) : ICommand
{
    public const string Extension = "*.collisions";
    public const string Prefix = "Collisions";
    public const string CommandKey = "Commands.Collision";

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
            () => CollisionOffset.Between((IGameItem)args[1], (IGameItem)args[2]),
            (ICommand)args[3]));
    }

    private string Key(string path) => Prefix + "." + string.Join('.',
        Path.GetRelativePath(directory, Path.ChangeExtension(path, null))
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

    private static void Register(string name, Func<object[], object> strategy)
    {
        Ioc.Resolve<App.ICommand>("IoC.Register", name, strategy).Execute();
    }
}
