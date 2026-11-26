using System.Text;
using SpaceBattle.Lib;

namespace SpaceBattle.Http;

internal static class BoundedProgramInput
{
    public static async Task<string> Read(Stream input, int limit, CancellationToken cancellationToken)
    {
        var bytes = new byte[limit + 1];
        var size = await input.ReadAtLeastAsync(bytes, bytes.Length, false, cancellationToken);
        if (size > limit) throw new PlayerProgramException("The program exceeded the data limit.");
        return new UTF8Encoding(false, true).GetString(bytes, 0, size);
    }
}
