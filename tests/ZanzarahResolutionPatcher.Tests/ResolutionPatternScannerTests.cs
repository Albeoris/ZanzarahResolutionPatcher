using System.Buffers.Binary;
using ZanzarahResolutionPatcher.Domain;
using ZanzarahResolutionPatcher.Services;

namespace ZanzarahResolutionPatcher.Tests;

public sealed class ResolutionPatternScannerTests
{
    private readonly ResolutionPatternScanner scanner = new();

    [Fact]
    public void FindAll_FindsEveryPatternInOnePass()
    {
        var resolution640 = new Resolution(640, 480);
        var resolution800 = new Resolution(800, 600);
        var bytes = Enumerable.Repeat((byte)0xFF, 40).ToArray();
        WritePattern(bytes, 1, resolution800);
        WritePattern(bytes, 10, resolution640);
        WritePattern(bytes, 22, resolution800);

        var result = scanner.FindAll(bytes, [resolution640, resolution800]);

        Assert.Equal([10], result[resolution640]);
        Assert.Equal([1, 22], result[resolution800]);
    }

    [Fact]
    public void FindAll_DoesNotMatchNonZeroUnusedField()
    {
        var resolution = new Resolution(800, 600);
        var bytes = new byte[Resolution.BinarySize];
        WritePattern(bytes, 0, resolution);
        bytes[2] = 1;

        var result = scanner.FindAll(bytes, [resolution]);

        Assert.Empty(result[resolution]);
    }

    private static void WritePattern(byte[] bytes, int offset, Resolution resolution)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), resolution.Width);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset + 2), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset + 4), resolution.Height);
    }
}
