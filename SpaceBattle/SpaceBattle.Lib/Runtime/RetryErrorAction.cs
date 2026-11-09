namespace SpaceBattle.Lib;

public sealed class RetryErrorAction : IErrorAction { public bool Apply(string gameId) => true; }
