using ZanzarahResolutionPatcher.Domain;
using ZanzarahResolutionPatcher.Services;

namespace ZanzarahResolutionPatcher.Tests;

public sealed class PatchMetadataCodecTests
{
    private static readonly Resolution[] Resolutions =
    [
        new(640, 480),
        new(1920, 1080),
        new(1024, 768),
    ];

    private readonly PatchMetadataCodec codec = new();

    [Fact]
    public void AppendAndRead_RoundTripsMetadata()
    {
        byte[] executable = [0x4D, 0x5A, 1, 2, 3];

        var file = codec.Append(executable, Resolutions);
        var metadata = codec.Read(file, out var executableLength);

        Assert.NotNull(metadata);
        Assert.Equal(executable.Length, executableLength);
        Assert.Equal(PatchMetadataCodec.CurrentVersion, metadata.Version);
        Assert.Equal(Resolutions, metadata.Resolutions);
        Assert.Equal("ZZRP"u8.ToArray(), file[^4..]);
    }

    [Fact]
    public void Read_WithoutMagic_ReturnsNoMetadata()
    {
        byte[] executable = [0x4D, 0x5A, 1, 2, 3];

        var metadata = codec.Read(executable, out var executableLength);

        Assert.Null(metadata);
        Assert.Equal(executable.Length, executableLength);
    }

    [Fact]
    public void Read_WithUnsupportedVersion_Throws()
    {
        var file = codec.Append([0x4D, 0x5A], Resolutions);
        file[^8] = 2;

        var exception = Assert.Throws<InvalidDataException>(() => codec.Read(file, out _));

        Assert.Contains("version 2", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Append_WithDuplicateResolutions_Throws()
    {
        Resolution[] duplicateResolutions =
        [
            new(640, 480),
            new(640, 480),
            new(1024, 768),
        ];

        var exception = Assert.Throws<ArgumentException>(
            () => codec.Append([0x4D, 0x5A], duplicateResolutions));

        Assert.Contains("unique", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
