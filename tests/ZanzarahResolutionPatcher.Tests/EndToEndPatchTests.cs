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

    private static byte[] CreateSyntheticExecutable()
    {
        var bytes = Enumerable.Repeat((byte)0xCC, 128).ToArray();
        bytes[0] = 0x4D;
        bytes[1] = 0x5A;

        WritePattern(bytes, 16, new Resolution(640, 480));
        WritePattern(bytes, 32, new Resolution(800, 600));
        WritePattern(bytes, 48, new Resolution(1024, 768));
        WritePattern(bytes, 64, new Resolution(640, 480));
        WritePattern(bytes, 80, new Resolution(800, 600));
        WritePattern(bytes, 96, new Resolution(1024, 768));
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
