using System.Collections.Concurrent;
namespace SpaceBattle.Lib;

public interface IErrorPolicy { ErrorAction Decide(Exception error, int attempt); }
