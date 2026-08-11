using System.Buffers.Binary;
using ZanzarahResolutionPatcher.Domain;
using ZanzarahResolutionPatcher.Services;

namespace ZanzarahResolutionPatcher.Tests;

public sealed class ResolutionPatternScannerTests
{
    private readonly ResolutionPatternScanner scanner = new();

    [Fact]
    public void FindAll_WithTwoUnsafeMatches_ReturnsOnlySharedSixOffsetLayout()
    {
        var resolution640 = new Resolution(640, 480);
        var resolution1920 = new Resolution(1920, 1080);
        var resolution1024 = new Resolution(1024, 768);
        int[] offsets640 = [1726712, 1726748, 1727568, 1727604, 1727648, 1727684];
        int[] offsets1920 = [1726724, 1726760, 1727580, 1727616, 1727660, 1727696];
        int[] safeOffsets1024 = [1726736, 1726772, 1727592, 1727628, 1727672, 1727708];
        int[] unsafeOffsets1024 = [905283, 909491];
        var bytes = Enumerable.Repeat((byte)0xFF, 1727714).ToArray();
        WritePatterns(bytes, offsets640, resolution640);
        WritePatterns(bytes, offsets1920, resolution1920);
        WritePatterns(bytes, safeOffsets1024, resolution1024);
        WritePatterns(bytes, unsafeOffsets1024, resolution1024);

        var result = scanner.FindAll(bytes, [resolution640, resolution1920, resolution1024]);

        Assert.Equal(offsets640, result[resolution640]);
        Assert.Equal(offsets1920, result[resolution1920]);
        Assert.Equal(safeOffsets1024, result[resolution1024]);
    }

    [Fact]
    public void FindAll_WithFewerThanSixSharedMatches_ExcludesResolutions()
    {
        var resolution640 = new Resolution(640, 480);
        var resolution800 = new Resolution(800, 600);
        var bytes = Enumerable.Repeat((byte)0xFF, 160).ToArray();
        WritePatterns(bytes, [10, 30, 50, 70, 90, 110], resolution640);
        WritePatterns(bytes, [16, 36, 56, 76, 96], resolution800);

        var result = scanner.FindAll(bytes, [resolution640, resolution800]);

        Assert.Empty(result);
    }

    [Fact]
    public void FindAll_WithDifferentSixOffsetLayouts_ExcludesResolutions()
    {
        var resolution640 = new Resolution(640, 480);
        var resolution800 = new Resolution(800, 600);
        var bytes = Enumerable.Repeat((byte)0xFF, 180).ToArray();
        WritePatterns(bytes, [10, 30, 50, 70, 90, 110], resolution640);
        WritePatterns(bytes, [16, 39, 62, 85, 108, 131], resolution800);

        var result = scanner.FindAll(bytes, [resolution640, resolution800]);

        Assert.Empty(result);
    }

    [Fact]
    public void FindAll_DoesNotMatchNonZeroUnusedField()
    {
        var resolution640 = new Resolution(640, 480);
        var resolution800 = new Resolution(800, 600);
        var bytes = Enumerable.Repeat((byte)0xFF, 160).ToArray();
        int[] offsets640 = [10, 30, 50, 70, 90, 110];
        int[] offsets800 = [16, 36, 56, 76, 96, 116];
        WritePatterns(bytes, offsets640, resolution640);
        WritePatterns(bytes, offsets800, resolution800);
        bytes[offsets800[2] + sizeof(ushort)] = 1;

        var result = scanner.FindAll(bytes, [resolution640, resolution800]);

        Assert.Empty(result);
    }

    private static void WritePatterns(byte[] bytes, IEnumerable<int> offsets, Resolution resolution)
    {
        foreach (var offset in offsets)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), resolution.Width);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset + 2), 0);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset + 4), resolution.Height);
        }
    }
}
