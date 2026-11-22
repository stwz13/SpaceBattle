namespace SpaceBattle.Lib;

public sealed record GameRulesProposal(long Revision, GameActionRules Rules, IReadOnlyList<string> ConfirmedBy,
    bool HasStarted);
