namespace SpaceBattle.Lib;

public static class SerializationDependencies
{
    public const string Codec = DependencyNamespaces.Serialization + ".Codec";
    public const string CodecRegistry = DependencyNamespaces.Serialization + ".CodecRegistry";
    public const string CommandSerializers = DependencyNamespaces.Serialization + ".CommandSerializers";
    public const string Snapshot = DependencyNamespaces.Games + ".Snapshot";
    public const string Restore = DependencyNamespaces.Games + ".Restore";
    public const string IdProperty = "Id";
    public const string KindProperty = "Kind";

    public static string Schema(string kind) => DependencyNamespaces.Serialization + ".Schema." + kind;
}
