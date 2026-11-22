namespace SpaceBattle.Lib;

internal static class BattleItemProperties
{
    public static T? Read<T>(IGameItem item, string name) where T : class
    {
        try { return item.GetProperty(name) as T; }
        catch (KeyNotFoundException) { return null; }
    }

    public static bool IsDestroyed(IGameItem item)
    {
        try { return item.GetProperty("Destroyed") is true; }
        catch (KeyNotFoundException) { return false; }
    }

}
