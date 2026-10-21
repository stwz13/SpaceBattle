using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class Sha256HashFunctionTests
{
    private readonly Sha256HashFunction _hash = new();

    [Fact]
    public void Hash_Is_The_First_Eight_Bytes_Of_Sha256_As_A_Big_Endian_Number()
    {
        Assert.Equal(0xBA7816BF8F01CFEAUL, _hash.Hash("abc"));
        Assert.Equal(0xE3B0C44298FC1C14UL, _hash.Hash(""));
    }

    [Fact]
    public void Hash_Does_Not_Depend_On_The_Instance_Or_The_Run()
    {
        Assert.Equal(_hash.Hash("Battle-42"), new Sha256HashFunction().Hash("Battle-42"));
        Assert.NotEqual(_hash.Hash("Battle-42"), _hash.Hash("Battle-43"));
    }

    [Fact]
    public void Text_Is_Hashed_As_Utf8()
    {
        Assert.Equal(0xCA978112CA1BBDCAUL, _hash.Hash("a"));
        Assert.NotEqual(_hash.Hash("игра"), _hash.Hash("igra"));
    }

    [Fact]
    public void Nothing_Cannot_Be_Hashed()
    {
        Assert.Throws<ArgumentNullException>(() => _hash.Hash(null!));
    }
}
