using System.Diagnostics;
using System.Text;

namespace SpaceBattle.Http;

internal sealed class ProgramProcess(TimeSpan drainTimeout) : IProgramProcess
{
    public async Task<(int ExitCode, string Output)> Run(string executable, IEnumerable<string> arguments, string input,
        int outputLimit, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false)
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("The container client could not start.");
        var write = Write(process, input, cancellationToken);
        var errors = process.StandardError.BaseStream.CopyToAsync(Stream.Null, cancellationToken);
        try
        {
            var output = await BoundedProgramInput.Read(process.StandardOutput.BaseStream, outputLimit, cancellationToken);
            await Task.WhenAll(write, errors, process.WaitForExitAsync(cancellationToken));
            return (process.ExitCode, output);
        }
        finally
        {
            if (!process.HasExited)
            {
                try { process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) when (process.HasExited) { }
            }
            var drain = Task.WhenAll(write, errors);
            await Task.WhenAny(drain, Task.Delay(drainTimeout));
            _ = drain.Exception;
        }
    }

    private static async Task Write(Process process, string input, CancellationToken cancellationToken)
    {
        try
        {
            using var writer = process.StandardInput;
            await writer.WriteAsync(input.AsMemory(), cancellationToken);
        }
        catch (IOException) { }
    }
}
