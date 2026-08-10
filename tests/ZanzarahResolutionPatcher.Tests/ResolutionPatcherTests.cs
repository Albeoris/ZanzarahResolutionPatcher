using System.Buffers.Binary;
using ZanzarahResolutionPatcher.Domain;
using ZanzarahResolutionPatcher.Services;

namespace ZanzarahResolutionPatcher.Tests;

public sealed class ResolutionPatcherTests
{
    [Fact]
    public void Patch_ReplacesSelectedPatternsAndAppendsUpdatedMetadata()
    {
        var oldResolution = new Resolution(800, 600);
        var newResolution = new Resolution(1920, 1080);
        Resolution[] gameResolutions = [new(640, 480), oldResolution, new(1024, 768)];
        var executable = new byte[48];
        WritePattern(executable, 4, oldResolution);
        WritePattern(executable, 20, oldResolution);

        var options = new PatchOptions
        {
            InputPath = "input.exe",
            OutputPath = "output.exe",
            OldResolution = oldResolution,
            NewResolution = newResolution,
        };
        options.SetReplacement(oldResolution, newResolution);
        var offsets = new Dictionary<Resolution, IReadOnlyList<int>>
        {
            [oldResolution] = [4, 20],
        };
        var codec = new PatchMetadataCodec();
        var plan = new PatchPlan(
            options,
            executable,
            executable.Length,
            null,
            gameResolutions,
            offsets,
            null,
            false);

        var result = new ResolutionPatcher(codec).Patch(plan);
        var metadata = codec.Read(result, out var executableLength);

        Assert.Equal(executable.Length, executableLength);
        Assert.Equal(newResolution.Width, BinaryPrimitives.ReadUInt16LittleEndian(result.AsSpan(4)));
        Assert.Equal(newResolution.Height, BinaryPrimitives.ReadUInt16LittleEndian(result.AsSpan(8)));
        Assert.Equal(newResolution.Width, BinaryPrimitives.ReadUInt16LittleEndian(result.AsSpan(20)));
        Assert.Equal([new Resolution(640, 480), newResolution, new Resolution(1024, 768)], metadata!.Resolutions);
    }

    [Fact]
    public void Patch_WithMultipleReplacements_UpdatesEverySelectedPatternAndMetadataRecord()
    {
        var resolution640 = new Resolution(640, 480);
        var resolution800 = new Resolution(800, 600);
        var resolution1024 = new Resolution(1024, 768);
        var resolution720p = new Resolution(1280, 720);
        var resolution1080p = new Resolution(1920, 1080);
        Resolution[] gameResolutions = [resolution640, resolution800, resolution1024];
        var executable = new byte[64];
        WritePattern(executable, 2, resolution640);
        WritePattern(executable, 16, resolution800);
        WritePattern(executable, 30, resolution1024);

        var options = new PatchOptions
        {
            InputPath = "input.exe",
            OutputPath = "output.exe",
        };
        options.SetReplacement(resolution640, resolution720p);
        options.SetReplacement(resolution1024, resolution1080p);

        var offsets = new Dictionary<Resolution, IReadOnlyList<int>>
        {
            [resolution640] = [2],
            [resolution800] = [16],
            [resolution1024] = [30],
        };
        var codec = new PatchMetadataCodec();
        var plan = new PatchPlan(
            options,
            executable,
            executable.Length,
            null,
            gameResolutions,
            offsets,
            null,
            false);

        var result = new ResolutionPatcher(codec).Patch(plan);
        var metadata = codec.Read(result, out _);

        Assert.Equal(resolution720p.Width, BinaryPrimitives.ReadUInt16LittleEndian(result.AsSpan(2)));
        Assert.Equal(resolution720p.Height, BinaryPrimitives.ReadUInt16LittleEndian(result.AsSpan(6)));
        Assert.Equal(resolution800.Width, BinaryPrimitives.ReadUInt16LittleEndian(result.AsSpan(16)));
        Assert.Equal(resolution1080p.Width, BinaryPrimitives.ReadUInt16LittleEndian(result.AsSpan(30)));
        Assert.Equal(
            [resolution720p, resolution800, resolution1080p],
            metadata!.Resolutions);
    }

    private static void WritePattern(byte[] bytes, int offset, Resolution resolution)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), resolution.Width);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset + 2), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset + 4), resolution.Height);
    }
}
