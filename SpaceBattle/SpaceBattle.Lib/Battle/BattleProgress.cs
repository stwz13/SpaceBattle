namespace SpaceBattle.Lib;

public sealed record BattleProgress(bool HasStarted, bool IsFinished, string? Winner);
