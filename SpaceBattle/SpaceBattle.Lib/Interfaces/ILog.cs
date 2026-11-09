using System.Collections.Concurrent;
namespace SpaceBattle.Lib;

public interface ILog { void Write(string gameId, Exception error, ErrorAction action, int attempt); }
