using System.Buffers.Binary;
using System.Windows.Forms;
using ZanzarahResolutionPatcher.Domain;
using ZanzarahResolutionPatcher.Services;

namespace ZanzarahResolutionPatcher.Tests;

public sealed class EndToEndPatchTests
{
    [Fact]
    public void Main_PatchesInPlaceCreatesOneBackupAndUsesMetadataOnSecondRun()
    {
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"ZanzarahResolutionPatcher.Tests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);

        try
        {
            var inputPath = Path.Combine(temporaryDirectory, "zanthp.exe");
            var originalBytes = CreateSyntheticExecutable();
            File.WriteAllBytes(inputPath, originalBytes);

            var firstExitCode = Program.Main(
            [
                "-i", inputPath,
                "-or", "800x600",
                "-nw", "1600",
                "-nh", "900",
                "-u",
                "-ni",
            ]);

            var backupPath = inputPath + "_resolution.bak";
            Assert.Equal(0, firstExitCode);
            Assert.Equal(HighDpiMode.PerMonitorV2, Application.HighDpiMode);
            Assert.True(File.Exists(backupPath));
            Assert.Equal(originalBytes, File.ReadAllBytes(backupPath));
            AssertMetadataContains(inputPath, new Resolution(1600, 900));

            var secondExitCode = Program.Main(
            [
                "--input", inputPath,
                "--old-resolution", "1600x900",
                "--new-resolution", "1920x1080",
                "--unchecked",
                "--non-interactive",
            ]);

            Assert.Equal(0, secondExitCode);
            Assert.Equal(originalBytes, File.ReadAllBytes(backupPath));
            AssertMetadataContains(inputPath, new Resolution(1920, 1080));
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public void Main_WhenPatchWouldCreateDuplicateResolution_RejectsWithoutChangingFile()
    {
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"ZanzarahResolutionPatcher.Tests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);

        try
        {
            var inputPath = Path.Combine(temporaryDirectory, "zanthp.exe");
            var originalBytes = CreateSyntheticExecutable();
            File.WriteAllBytes(inputPath, originalBytes);

            var exitCode = Program.Main(
            [
                "--input", inputPath,
                "--old-resolution", "800x600",
                "--new-resolution", "640x480",
                "--unchecked",
                "--non-interactive",
            ]);

            Assert.Equal(1, exitCode);
            Assert.Equal(originalBytes, File.ReadAllBytes(inputPath));
            Assert.False(File.Exists(inputPath + "_resolution.bak"));
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    private static byte[] CreateSyntheticExecutable()
    {
        var bytes = Enumerable.Repeat((byte)0xCC, 256).ToArray();
        bytes[0] = 0x4D;
        bytes[1] = 0x5A;

        int[] groupOffsets = [8, 44, 96, 132, 176, 212];
        foreach (var groupOffset in groupOffsets)
        {
            WritePattern(bytes, groupOffset, new Resolution(640, 480));
            WritePattern(bytes, groupOffset + 12, new Resolution(800, 600));
            WritePattern(bytes, groupOffset + 24, new Resolution(1024, 768));
        }

        return bytes;
    }

    private static void AssertMetadataContains(string path, Resolution expected)
    {
        var file = File.ReadAllBytes(path);
        var metadata = new PatchMetadataCodec().Read(file, out _);

        Assert.NotNull(metadata);
        Assert.Contains(expected, metadata.Resolutions);
    }

    private static void WritePattern(byte[] bytes, int offset, Resolution resolution)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), resolution.Width);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset + 2), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset + 4), resolution.Height);
    }
}
