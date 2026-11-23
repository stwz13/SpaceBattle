namespace SpaceBattle.Http;

public interface IProgramProcess
{
    Task<(int ExitCode, string Output)> Run(string executable, IEnumerable<string> arguments, string input,
        int outputLimit, CancellationToken cancellationToken);
}
