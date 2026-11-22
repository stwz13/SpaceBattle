namespace SpaceBattle.Lib;

internal sealed class FleetObjectAccessAreas : IObjectAccessAreas
{
    private readonly IReadOnlySet<string> _participants;

    public FleetObjectAccessAreas(IReadOnlySet<string> participants)
    {
        ArgumentNullException.ThrowIfNull(participants);
        _participants = participants;
    }

    public string? FindArea(IGameItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        try
        {
            return item.GetProperty("Owner") is string owner && _participants.Contains(owner) ? For(owner) : null;
        }
        catch (KeyNotFoundException) { return null; }
    }

    internal static string For(string player) => "fleet." + player;
}
