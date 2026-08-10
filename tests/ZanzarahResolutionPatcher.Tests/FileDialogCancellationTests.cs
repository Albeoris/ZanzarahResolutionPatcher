using Spectre.Console;
using ZanzarahResolutionPatcher.Domain;
using ZanzarahResolutionPatcher.Services;

namespace ZanzarahResolutionPatcher.Tests;

public sealed class FileDialogCancellationTests
{
    [Fact]
    public void Run_WhenInputDialogIsCancelled_ExitsSuccessfullyWithoutContinuing()
    {
        var (application, output) = CreateApplication(new StubFileDialogService(null, null));

        var exitCode = application.Run(new PatchOptions());

        Assert.Equal(1, exitCode);
        Assert.Contains("Operation cancelled by the user.", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Run_WhenOutputDialogIsCancelled_ExitsSuccessfullyWithoutContinuing()
    {
        var inputPath = Path.GetTempFileName();

        try
        {
            var dialogs = new StubFileDialogService(inputPath, null);
            var (application, output) = CreateApplication(dialogs);

            var exitCode = application.Run(new PatchOptions());

            Assert.Equal(1, exitCode);
            Assert.Contains("Operation cancelled by the user.", output.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(inputPath);
        }
    }

    private static (PatchApplication Application, StringWriter Output) CreateApplication(
        IFileDialogService fileDialogs)
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
                fileDialogs,
                new EmptySupportedResolutionProvider(),
                metadataCodec,
                new ResolutionPatternScanner(),
                new ResolutionPatcher(metadataCodec),
                new PatchedFileWriter(),
                new StatusPresenter(console, fieldOfViewCalculator),
                fieldOfViewCalculator),
            output);
    }

    private sealed class StubFileDialogService(string? inputPath, string? outputPath) : IFileDialogService
    {
        public string? SelectInputFile() => inputPath;

        public string? SelectOutputFile(string inputPath) => outputPath;
    }

    private sealed class EmptySupportedResolutionProvider : ISupportedResolutionProvider
    {
        public IReadOnlyList<Resolution> GetSupportedResolutions() => [];
    }
}
