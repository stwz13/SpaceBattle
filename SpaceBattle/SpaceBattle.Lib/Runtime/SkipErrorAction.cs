namespace SpaceBattle.Lib;

public sealed class SkipErrorAction : IErrorAction { public bool Apply(string gameId) => false; }
