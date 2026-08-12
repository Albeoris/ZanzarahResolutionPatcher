using System.Buffers.Binary;
using AsmResolver.PE.File;
using Iced.Intel;
using Spectre.Console;
using Spectre.Console.Rendering;
using ZanzarahResolutionPatcher.Domain;
using ZanzarahResolutionPatcher.Services;

namespace ZanzarahResolutionPatcher.Tests;

public sealed class FieldOfViewPatcherTests
{
    private readonly FieldOfViewPatcher patcher = new();

    [Fact]
    public void Analyze_WithOriginalFunctionAtPhysicalOffset_ReportsAvailable()
    {
        var executable = TestPeFactory.Create();

        var status = patcher.Analyze(executable);

        Assert.Equal(FieldOfViewPatchStatus.Available, status);
    }

    [Fact]
    public void Apply_AddsExecutableSectionAndRedirectsOriginalFunction()
    {
        var executable = TestPeFactory.Create();
        var originalFile = PEFile.FromBytes(executable);

        var result = patcher.Apply(executable);

        var patchedFile = PEFile.FromBytes(result);
        var fovSection = Assert.Single(
            patchedFile.Sections,
            section => section.Name?.ToString() == FieldOfViewPatcher.SectionName);
        Assert.Equal(originalFile.Sections.Count + 1, patchedFile.Sections.Count);
        Assert.True(patchedFile.OptionalHeader.SizeOfImage > originalFile.OptionalHeader.SizeOfImage);
        Assert.Equal(0x100u, fovSection.GetVirtualSize());
        Assert.Equal(
            SectionFlags.ContentCode | SectionFlags.MemoryExecute | SectionFlags.MemoryRead,
            fovSection.Characteristics);
        AssertValidX86Function(result, fovSection);

        var jump = result.AsSpan(
            FieldOfViewPatcher.OriginalFunctionOffset,
            TestPeFactory.OriginalFunctionLength);
        Assert.Equal(0xE9, jump[0]);
        Assert.All(jump[5..].ToArray(), value => Assert.Equal(0xCC, value));

        Assert.True(
            patchedFile.TryGetSectionContainingOffset(
                FieldOfViewPatcher.OriginalFunctionOffset,
                out var originalSection));
        var sourceRva = originalSection.FileOffsetToRva(FieldOfViewPatcher.OriginalFunctionOffset);
        var displacement = BinaryPrimitives.ReadInt32LittleEndian(jump[1..]);
        Assert.Equal((long)fovSection.Rva, sourceRva + 5L + displacement);
        Assert.Equal(FieldOfViewPatchStatus.AlreadyApplied, patcher.Analyze(result));
    }

    [Fact]
    public void Apply_LeavesRoomForResolutionMetadataAfterThePeImage()
    {
        var patchedExecutable = patcher.Apply(TestPeFactory.Create());
        Resolution[] resolutions = [new(640, 480), new(1920, 1080), new(1024, 768)];
        var codec = new PatchMetadataCodec();

        var completeFile = codec.Append(patchedExecutable, resolutions);
        var metadata = codec.Read(completeFile, out var executableLength);

        Assert.Equal(patchedExecutable.Length, executableLength);
        Assert.Equal(resolutions, metadata!.Resolutions);
        Assert.Equal(
            FieldOfViewPatchStatus.AlreadyApplied,
            patcher.Analyze(completeFile.AsSpan(0, executableLength)));
    }

    [Fact]
    public void Apply_WithoutSpaceForAnotherSectionHeader_Throws()
    {
        var executable = TestPeFactory.Create(firstSectionRawOffset: 0x1A0, fileAlignment: 0x20);

        var exception = Assert.Throws<InvalidDataException>(() => patcher.Apply(executable));

        Assert.Contains("40 free bytes", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Analyze_WhenFunctionIsNotAtRequiredPhysicalOffset_ReportsNotAvailable()
    {
        var executable = TestPeFactory.Create();
        executable[FieldOfViewPatcher.OriginalFunctionOffset] = 0x90;

        var status = patcher.Analyze(executable);

        Assert.Equal(FieldOfViewPatchStatus.NotAvailable, status);
    }

    [Fact]
    public void Run_WhenFixIsAlreadyApplied_DoesNotPromptOrShowConsoleFovWarning()
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
            File.WriteAllBytes(inputPath, patcher.Apply(TestPeFactory.Create()));
            var output = new StringWriter();
            var innerConsole = AnsiConsole.Create(
                new AnsiConsoleSettings
                {
                    Ansi = AnsiSupport.No,
                    ColorSystem = ColorSystemSupport.NoColors,
                    Out = new AnsiConsoleOutput(output),
                    Interactive = InteractionSupport.Yes,
                });
            var console = new TestConsole(
                innerConsole,
                new QueueConsoleInput('y', '\r', '\r'));
            var metadataCodec = new PatchMetadataCodec();
            var calculator = new FieldOfViewCalculator();
            var application = new PatchApplication(
                console,
                new UnusedFileDialogService(),
                new EmptySupportedResolutionProvider(),
                metadataCodec,
                new ResolutionPatternScanner(),
                new ResolutionPatcher(metadataCodec),
                new PatchedFileWriter(),
                new StatusPresenter(console, calculator),
                calculator,
                patcher);
            var options = new PatchOptions
            {
                InputPath = inputPath,
                OutputPath = outputPath,
                OldResolution = new Resolution(800, 600),
                NewResolution = new Resolution(1920, 1080),
                IsUnchecked = true,
                NoBackup = true,
                ApplyFieldOfViewFix = true,
            };

            var exitCode = application.Run(options);
            var renderedOutput = output.ToString();
            var completeFile = File.ReadAllBytes(outputPath);
            _ = metadataCodec.Read(completeFile, out var executableLength);

            Assert.Equal(0, exitCode);
            Assert.Contains("already enabled", renderedOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("FOV REQUIRED", renderedOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("after every game launch", renderedOutput, StringComparison.Ordinal);
            Assert.Contains("Alt+Tab", renderedOutput, StringComparison.Ordinal);
            Assert.Contains("Press ENTER to exit", renderedOutput, StringComparison.Ordinal);
            Assert.Equal(
                FieldOfViewPatchStatus.AlreadyApplied,
                patcher.Analyze(completeFile.AsSpan(0, executableLength)));

            var secondExitCode = Program.Main(
            [
                "--input", outputPath,
                "--old-resolution", "1920x1080",
                "--new-resolution", "1600x900",
                "--unchecked",
                "--no-backup",
                "--non-interactive",
                "--fov-fix",
            ]);

            Assert.Equal(0, secondExitCode);
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public void Run_InteractiveWidescreenPatch_WhenFovFixIsRequired_AppliesWithoutFovConfirmation()
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
            File.WriteAllBytes(inputPath, TestPeFactory.Create());
            var output = new StringWriter();
            var innerConsole = AnsiConsole.Create(
                new AnsiConsoleSettings
                {
                    Ansi = AnsiSupport.No,
                    ColorSystem = ColorSystemSupport.NoColors,
                    Out = new AnsiConsoleOutput(output),
                    Interactive = InteractionSupport.Yes,
                });
            var console = new TestConsole(
                innerConsole,
                new QueueConsoleInput('y', '\r', '\r'));
            var metadataCodec = new PatchMetadataCodec();
            var calculator = new FieldOfViewCalculator();
            var application = new PatchApplication(
                console,
                new UnusedFileDialogService(),
                new EmptySupportedResolutionProvider(),
                metadataCodec,
                new ResolutionPatternScanner(),
                new ResolutionPatcher(metadataCodec),
                new PatchedFileWriter(),
                new StatusPresenter(console, calculator),
                calculator,
                patcher);
            var options = new PatchOptions
            {
                InputPath = inputPath,
                OutputPath = outputPath,
                OldResolution = new Resolution(800, 600),
                NewResolution = new Resolution(1920, 1080),
                IsUnchecked = true,
                NoBackup = true,
                ApplyFieldOfViewFix = true,
            };

            var exitCode = application.Run(options);
            var completeFile = File.ReadAllBytes(outputPath);
            _ = metadataCodec.Read(completeFile, out var executableLength);
            var renderedOutput = output.ToString();

            Assert.Equal(0, exitCode);
            Assert.DoesNotContain("Apply the automatic FOV fix?", renderedOutput, StringComparison.Ordinal);
            Assert.Contains("automatic calculation was enabled", renderedOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("FOV REQUIRED", renderedOutput, StringComparison.Ordinal);
            Assert.Contains("Alt+Tab", renderedOutput, StringComparison.Ordinal);
            Assert.Contains("Press ENTER to exit", renderedOutput, StringComparison.Ordinal);
            Assert.Equal(
                FieldOfViewPatchStatus.AlreadyApplied,
                patcher.Analyze(completeFile.AsSpan(0, executableLength)));
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public void Main_NonInteractiveFovFix_AppliesFixWithoutPrompting()
    {
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"ZanzarahResolutionPatcher.Tests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);

        try
        {
            var inputPath = Path.Combine(temporaryDirectory, "zanthp.exe");
            var outputPath = Path.Combine(temporaryDirectory, "patched.exe");
            File.WriteAllBytes(inputPath, TestPeFactory.Create());

            var exitCode = Program.Main(
            [
                "--input", inputPath,
                "--output", outputPath,
                "--old-resolution", "800x600",
                "--new-resolution", "1920x1080",
                "--unchecked",
                "--no-backup",
                "--non-interactive",
                "--fov-fix",
            ]);

            var completeFile = File.ReadAllBytes(outputPath);
            _ = new PatchMetadataCodec().Read(completeFile, out var executableLength);

            Assert.Equal(0, exitCode);
            Assert.Equal(
                FieldOfViewPatchStatus.AlreadyApplied,
                patcher.Analyze(completeFile.AsSpan(0, executableLength)));
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public void Main_RequiredFovFixIsUnavailable_ReturnsErrorWithoutWritingOutput()
    {
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"ZanzarahResolutionPatcher.Tests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);

        try
        {
            var inputPath = Path.Combine(temporaryDirectory, "zanthp.exe");
            var outputPath = Path.Combine(temporaryDirectory, "patched.exe");
            var executable = TestPeFactory.Create();
            executable[FieldOfViewPatcher.OriginalFunctionOffset] = 0x90;
            File.WriteAllBytes(inputPath, executable);

            var exitCode = Program.Main(
            [
                "--input", inputPath,
                "--output", outputPath,
                "--old-resolution", "800x600",
                "--new-resolution", "1920x1080",
                "--unchecked",
                "--no-backup",
                "--non-interactive",
                "--fov-fix",
            ]);

            Assert.Equal(1, exitCode);
            Assert.False(File.Exists(outputPath));
            Assert.Equal(executable, File.ReadAllBytes(inputPath));
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public void Run_InteractiveUncheckedInput_WhenResolutionIsAlreadyUsed_RepromptsImmediately()
    {
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"ZanzarahResolutionPatcher.Tests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);

        try
        {
            var inputPath = Path.Combine(temporaryDirectory, "zanthp.exe");
            var outputPath = Path.Combine(temporaryDirectory, "patched.exe");
            File.WriteAllBytes(inputPath, TestPeFactory.Create());
            var (application, output) = CreateInteractiveApplication(
                new QueueConsoleInput(
                    '8', '0', '0', '\r',
                    '6', '0', '0', '\r',
                    '1', '2', '8', '0', '\r',
                    '7', '2', '0', '\r',
                    'y', '\r',
                    'n', '\r',
                    '\r'),
                new EmptySupportedResolutionProvider());
            var options = new PatchOptions
            {
                InputPath = inputPath,
                OutputPath = outputPath,
                OldResolution = new Resolution(640, 480),
                IsUnchecked = true,
                NoBackup = true,
            };

            var exitCode = application.Run(options);
            var metadata = new PatchMetadataCodec().Read(File.ReadAllBytes(outputPath), out _);

            Assert.Equal(0, exitCode);
            Assert.Contains("already used by another game resolution", output.ToString(), StringComparison.Ordinal);
            Assert.Contains(new Resolution(1280, 720), metadata!.Resolutions);
            Assert.Equal(metadata.Resolutions.Count, metadata.Resolutions.Distinct().Count());
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    private static class TestPeFactory
    {
        public const int OriginalFunctionLength = 42;

        private const int PeHeaderOffset = 0x80;
        private const int OptionalHeaderSize = 0xE0;
        private const int SectionHeaderOffset = PeHeaderOffset + 4 + 20 + OptionalHeaderSize;
        private const int SectionVirtualAddress = 0x1000;
        private const int SectionRawSize = 0x7600;

        private static readonly byte[] OriginalFunction =
        [
            0x55, 0x8B, 0xEC, 0x51, 0x51, 0x8B, 0x45, 0x08,
            0x89, 0x45, 0xF8, 0x8B, 0x45, 0x0C, 0x56, 0x89,
            0x45, 0xFC, 0x8D, 0x45, 0xF8, 0x8B, 0xF1, 0x50,
            0xFF, 0x76, 0x18, 0xE8, 0x51, 0x2A, 0x0E, 0x00,
            0x59, 0x59, 0x89, 0x46, 0x18, 0x5E, 0xC9, 0xC2,
            0x08, 0x00,
        ];

        public static byte[] Create(int firstSectionRawOffset = 0x200, int fileAlignment = 0x200)
        {
            var bytes = new byte[firstSectionRawOffset + SectionRawSize];
            WriteUInt16(bytes, 0, 0x5A4D);
            WriteUInt32(bytes, 0x3C, PeHeaderOffset);

            WriteUInt32(bytes, PeHeaderOffset, PEFile.ValidPESignature);
            var fileHeaderOffset = PeHeaderOffset + sizeof(uint);
            WriteUInt16(bytes, fileHeaderOffset, (ushort)MachineType.I386);
            WriteUInt16(bytes, fileHeaderOffset + 2, 1);
            WriteUInt16(bytes, fileHeaderOffset + 16, OptionalHeaderSize);
            WriteUInt16(bytes, fileHeaderOffset + 18, 0x0102);

            var optionalHeaderOffset = fileHeaderOffset + 20;
            WriteUInt16(bytes, optionalHeaderOffset, (ushort)OptionalHeaderMagic.PE32);
            WriteUInt32(bytes, optionalHeaderOffset + 4, SectionRawSize);
            WriteUInt32(bytes, optionalHeaderOffset + 16, SectionVirtualAddress);
            WriteUInt32(bytes, optionalHeaderOffset + 20, SectionVirtualAddress);
            WriteUInt32(bytes, optionalHeaderOffset + 28, 0x00400000);
            WriteUInt32(bytes, optionalHeaderOffset + 32, 0x1000);
            WriteUInt32(bytes, optionalHeaderOffset + 36, fileAlignment);
            WriteUInt16(bytes, optionalHeaderOffset + 40, 4);
            WriteUInt16(bytes, optionalHeaderOffset + 48, 4);
            WriteUInt32(bytes, optionalHeaderOffset + 56, 0x9000);
            WriteUInt32(bytes, optionalHeaderOffset + 60, firstSectionRawOffset);
            WriteUInt16(bytes, optionalHeaderOffset + 68, 2);
            WriteUInt32(bytes, optionalHeaderOffset + 72, 0x00100000);
            WriteUInt32(bytes, optionalHeaderOffset + 76, 0x1000);
            WriteUInt32(bytes, optionalHeaderOffset + 80, 0x00100000);
            WriteUInt32(bytes, optionalHeaderOffset + 84, 0x1000);
            WriteUInt32(bytes, optionalHeaderOffset + 92, 16);

            ".text"u8.CopyTo(bytes.AsSpan(SectionHeaderOffset));
            WriteUInt32(bytes, SectionHeaderOffset + 8, SectionRawSize);
            WriteUInt32(bytes, SectionHeaderOffset + 12, SectionVirtualAddress);
            WriteUInt32(bytes, SectionHeaderOffset + 16, SectionRawSize);
            WriteUInt32(bytes, SectionHeaderOffset + 20, firstSectionRawOffset);
            WriteUInt32(
                bytes,
                SectionHeaderOffset + 36,
                (uint)(SectionFlags.ContentCode | SectionFlags.MemoryExecute | SectionFlags.MemoryRead));

            int[] patternGroupOffsets = [0x300, 0x340, 0x380, 0x3C0, 0x400, 0x440];
            foreach (var groupOffset in patternGroupOffsets)
            {
                WriteResolutionPattern(bytes, groupOffset, new Resolution(640, 480));
                WriteResolutionPattern(bytes, groupOffset + 12, new Resolution(800, 600));
                WriteResolutionPattern(bytes, groupOffset + 24, new Resolution(1024, 768));
            }

            OriginalFunction.CopyTo(bytes, FieldOfViewPatcher.OriginalFunctionOffset);
            return bytes;
        }

        private static void WriteResolutionPattern(byte[] bytes, int offset, Resolution resolution)
        {
            WriteUInt16(bytes, offset, resolution.Width);
            WriteUInt16(bytes, offset + sizeof(ushort), 0);
            WriteUInt16(bytes, offset + (sizeof(ushort) * 2), resolution.Height);
        }

        private static void WriteUInt16(byte[] bytes, int offset, int value) =>
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), checked((ushort)value));

        private static void WriteUInt32(byte[] bytes, int offset, int value) =>
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), checked((uint)value));

        private static void WriteUInt32(byte[] bytes, int offset, uint value) =>
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
    }

    private static void AssertValidX86Function(byte[] executable, PESection section)
    {
        var contents = section.Contents
            ?? throw new InvalidDataException("The FOV section does not contain code.");
        var codeSize = checked((int)contents.GetPhysicalSize());
        var code = executable.AsSpan(checked((int)section.Offset), codeSize).ToArray();
        var decoder = Decoder.Create(32, new ByteArrayCodeReader(code));
        decoder.IP = section.Rva;
        var endAddress = decoder.IP + (uint)code.Length;
        var instructions = new List<Instruction>();

        while (decoder.IP < endAddress)
        {
            var instruction = decoder.Decode();
            Assert.NotEqual(Code.INVALID, instruction.Code);
            instructions.Add(instruction);
            if (instruction.Mnemonic == Mnemonic.Ret)
            {
                break;
            }
        }

        Assert.NotEmpty(instructions);
        Assert.Equal(Mnemonic.Ret, instructions[^1].Mnemonic);
        Assert.Equal(92UL, decoder.IP - section.Rva);
        Assert.All(code[(int)(decoder.IP - section.Rva)..], value => Assert.Equal(0, value));

        var instructionAddresses = instructions
            .Select(static instruction => instruction.IP)
            .ToHashSet();
        foreach (var instruction in instructions.Where(static instruction =>
                     instruction.FlowControl is FlowControl.ConditionalBranch or FlowControl.UnconditionalBranch))
        {
            Assert.Contains(instruction.NearBranchTarget, instructionAddresses);
        }
    }

    private (PatchApplication Application, StringWriter Output) CreateInteractiveApplication(
        IAnsiConsoleInput input,
        ISupportedResolutionProvider supportedResolutionProvider)
    {
        var output = new StringWriter();
        var innerConsole = AnsiConsole.Create(
            new AnsiConsoleSettings
            {
                Ansi = AnsiSupport.Yes,
                ColorSystem = ColorSystemSupport.Standard,
                Out = new AnsiConsoleOutput(output),
                Interactive = InteractionSupport.Yes,
            });
        var console = new TestConsole(innerConsole, input);
        var metadataCodec = new PatchMetadataCodec();
        var calculator = new FieldOfViewCalculator();

        return (
            new PatchApplication(
                console,
                new UnusedFileDialogService(),
                supportedResolutionProvider,
                metadataCodec,
                new ResolutionPatternScanner(),
                new ResolutionPatcher(metadataCodec),
                new PatchedFileWriter(),
                new StatusPresenter(console, calculator),
                calculator,
                patcher),
            output);
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

    private sealed class TestConsole(IAnsiConsole inner, IAnsiConsoleInput input) : IAnsiConsole
    {
        public Profile Profile => inner.Profile;

        public IAnsiConsoleCursor Cursor => inner.Cursor;

        public IAnsiConsoleInput Input { get; } = input;

        public IExclusivityMode ExclusivityMode => inner.ExclusivityMode;

        public RenderPipeline Pipeline => inner.Pipeline;

        public void Clear(bool home) => inner.Clear(home);

        public void Write(IRenderable renderable) => inner.Write(renderable);

        public void WriteAnsi(Action<AnsiWriter> action) => inner.WriteAnsi(action);
    }

    private sealed class QueueConsoleInput(params char[] characters) : IAnsiConsoleInput
    {
        private readonly Queue<ConsoleKeyInfo> keys = new(
            characters.Select(CreateKeyInfo));

        public bool IsKeyAvailable() => keys.Count > 0;

        public ConsoleKeyInfo? ReadKey(bool intercept) =>
            keys.Count > 0 ? keys.Dequeue() : null;

        public Task<ConsoleKeyInfo?> ReadKeyAsync(
            bool intercept,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(ReadKey(intercept));
        }

        private static ConsoleKeyInfo CreateKeyInfo(char character)
        {
            var key = character switch
            {
                '\r' => ConsoleKey.Enter,
                >= '0' and <= '9' => (ConsoleKey)((int)ConsoleKey.D0 + (character - '0')),
                _ => Enum.Parse<ConsoleKey>(character.ToString(), ignoreCase: true),
            };
            return new ConsoleKeyInfo(character, key, shift: false, alt: false, control: false);
        }
    }
}
