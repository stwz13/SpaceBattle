using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace SpaceBattle.Lib;

public class Sha256HashFunction : IHashFunction
{
    public ulong Hash(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return BinaryPrimitives.ReadUInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
    }
}
