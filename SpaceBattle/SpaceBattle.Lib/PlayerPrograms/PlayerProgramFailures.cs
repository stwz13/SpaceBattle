namespace SpaceBattle.Lib;

public sealed class PlayerProgramException(string message) : Exception(message);

public sealed class ProgramSandboxUnavailableException(string message, Exception? inner = null) : Exception(message, inner);
