namespace SpaceBattle.Lib;

internal sealed record GameActivation(Action Activate, Action Suspend)
{
    public static GameActivation None { get; } = new(() => { }, () => { });
}
