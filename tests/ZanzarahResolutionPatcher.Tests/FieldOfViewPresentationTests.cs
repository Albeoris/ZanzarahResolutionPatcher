using System.Buffers.Binary;
using Spectre.Console;
using ZanzarahResolutionPatcher.Domain;
using ZanzarahResolutionPatcher.Services;

namespace ZanzarahResolutionPatcher.Tests;

public sealed class FieldOfViewPresentationTests
{
    [Fact]
    public void Run_WithWidescreenTarget_ShowsCalculatedFovAndPostPatchWarning()
    {
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"ZanzarahResolutionPatcher.Tests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);

        try
        {
            var systemDirectory = Path.Combine(
                temporaryDirectory,
                "steamapps",
                "common",
                "ZanZarah",
                "System");
            Directory.CreateDirectory(systemDirectory);
            var inputPath = Path.Combine(systemDirectory, "zanthp.exe");
            var outputPath = Path.Combine(temporaryDirectory, "patched.exe");
            File.WriteAllBytes(inputPath, CreateSyntheticExecutable());
            var (application, output) = CreateApplication();
            var options = new PatchOptions
            {
                InputPath = inputPath,
                OutputPath = outputPath,
                OldResolution = new Resolution(800, 600),
                NewResolution = new Resolution(1920, 1080),
                IsUnchecked = true,
                NonInteractive = true,
            };

            var exitCode = application.Run(options);
            var renderedOutput = output.ToString();
            var normalizedOutput = string.Join(
                " ",
                renderedOutput.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

            Assert.Equal(0, exitCode);
            Assert.Contains("1920x1080 (fov 1350,750)", renderedOutput, StringComparison.Ordinal);
            Assert.Contains("1920x1080: fov 1350,750", renderedOutput, StringComparison.Ordinal);
            Assert.Contains("after every game launch", normalizedOutput, StringComparison.Ordinal);
            Assert.Contains("Alt+Tab", renderedOutput, StringComparison.Ordinal);
            Assert.Contains("not caused by the resolution patch", normalizedOutput, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    private static (PatchApplication Application, StringWriter Output) CreateApplication()
    {
        var output = new StringWriter();
        var console = AnsiConsole.Create(
            new AnsiConsoleSettings
            {
                Ansi = AnsiSupport.No,
                ColorSystem = ColorSystemSupport.NoColors,
                Out = new AnsiConsoleOutput(output),
            });
        var metadataCodec = new PatchMetadataCodec();
        var fieldOfViewCalculator = new FieldOfViewCalculator();

        return (
            new PatchApplication(
                console,
                new UnusedFileDialogService(),
                new EmptySupportedResolutionProvider(),
                metadataCodec,
                new ResolutionPatternScanner(),
                new ResolutionPatcher(metadataCodec),
                new PatchedFileWriter(),
                new StatusPresenter(console, fieldOfViewCalculator),
                fieldOfViewCalculator),
            output);
    }

    private static byte[] CreateSyntheticExecutable()
    {
        var bytes = Enumerable.Repeat((byte)0xCC, 64).ToArray();
        WritePattern(bytes, 8, new Resolution(640, 480));
        WritePattern(bytes, 24, new Resolution(800, 600));
        WritePattern(bytes, 40, new Resolution(1024, 768));
        return bytes;
    }

    private static void WritePattern(byte[] bytes, int offset, Resolution resolution)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), resolution.Width);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset + 2), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset + 4), resolution.Height);
    }

    private sealed class UnusedFileDialogService : IFileDialogService
    {
        public string? SelectInputFile() => throw new InvalidOperationException();

        public string? SelectOutputFile(string inputPath) => throw new InvalidOperationException();
    }

    private sealed class EmptySupportedResolutionProvider : ISupportedResolutionProvider
    {
        public IReadOnlyList<Resolution> GetSupportedResolutions() => [];
    }
}
