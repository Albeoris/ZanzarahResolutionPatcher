using Spectre.Console;
using ZanzarahResolutionPatcher.Domain;
using ZanzarahResolutionPatcher.Infrastructure;

namespace ZanzarahResolutionPatcher.Services;

public sealed class PatchApplication(
    IAnsiConsole console,
    IFileDialogService fileDialogs,
    ISupportedResolutionProvider supportedResolutionProvider,
    PatchMetadataCodec metadataCodec,
    ResolutionPatternScanner patternScanner,
    ResolutionPatcher patcher,
    PatchedFileWriter fileWriter,
    StatusPresenter statusPresenter,
    FieldOfViewCalculator fieldOfViewCalculator,
    FieldOfViewPatcher? fieldOfViewPatcher = null)
{
    private readonly FieldOfViewPatcher fieldOfViewPatcher = fieldOfViewPatcher ?? new FieldOfViewPatcher();

    private static readonly Resolution[] OriginalGameResolutions =
    [
        new(640, 480),
        new(800, 600),
        new(1024, 768),
    ];

    public static PatchApplication CreateDefault()
    {
        var metadataCodec = new PatchMetadataCodec();
        var console = AnsiConsole.Console;
        var fieldOfViewCalculator = new FieldOfViewCalculator();

        return new PatchApplication(
            console,
            new FileDialogService(),
            new SupportedResolutionProvider(),
            metadataCodec,
            new ResolutionPatternScanner(),
            new ResolutionPatcher(metadataCodec),
            new PatchedFileWriter(),
            new StatusPresenter(console, fieldOfViewCalculator),
            fieldOfViewCalculator,
            new FieldOfViewPatcher());
    }

    public int Run(PatchOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!ResolvePaths(options))
        {
            console.MarkupLine("[yellow]Operation cancelled by the user.[/]");
            return 1;
        }

        EnsureNonInteractiveOptionsAreComplete(options);

        var sourceBytes = ReadInputFile(options.InputPath!);
        var metadata = ReadMetadata(sourceBytes, out var executableLength);
        var fieldOfViewPatchStatus = AnalyzeFieldOfViewPatch(sourceBytes.AsSpan(0, executableLength));
        var gameResolutions = metadata?.Resolutions ?? OriginalGameResolutions;
        var offsets = patternScanner.FindAll(
            sourceBytes.AsSpan(0, executableLength),
            gameResolutions);
        var patchableResolutions = GetPatchableResolutions(gameResolutions, offsets);

        var (backupPath, willCreateBackup, backupStatus) = ResolveBackup(options, metadata);

        var useMultipleReplacementWorkflow =
            !options.NonInteractive &&
            options.OldResolution is null &&
            options.NewResolution is null;

        if (useMultipleReplacementWorkflow)
        {
            if (!ResolveInteractiveReplacements(
                    options,
                    patchableResolutions,
                    gameResolutions,
                    backupStatus))
            {
                console.MarkupLine("[yellow]Operation cancelled by the user.[/]");
                return 1;
            }
        }
        else
        {
            ResolveOldResolution(options, patchableResolutions, backupStatus);
            ResolveNewResolution(options, gameResolutions, backupStatus);

            if (options.NewResolution == options.OldResolution)
            {
                throw new UserInputException("The old and new resolutions must be different.");
            }

            options.SetReplacement(options.OldResolution!.Value, options.NewResolution!.Value);
        }

        EnsureFinalResolutionsAreUnique(options, gameResolutions);
        var finalResolutions = options.ResolveFinalResolutions(gameResolutions);

        var plan = new PatchPlan(
            options,
            sourceBytes,
            executableLength,
            metadata,
            gameResolutions,
            offsets,
            backupPath,
            willCreateBackup);

        statusPresenter.Show(options, backupStatus);
        if (!options.NonInteractive &&
            !console.Confirm("Patch the executable with these settings?", defaultValue: false))
        {
            console.MarkupLine("[yellow]Operation cancelled. No files were changed.[/]");
            return 0;
        }

        var patchedExecutable = patcher.PatchExecutable(plan);
        var fieldOfViewPatchApplied = TryApplyFieldOfViewPatch(
            options,
            finalResolutions,
            fieldOfViewPatchStatus,
            ref patchedExecutable);
        var patchedBytes = metadataCodec.Append(patchedExecutable, finalResolutions);
        fileWriter.Write(
            options.OutputPath!,
            patchedBytes,
            backupPath,
            willCreateBackup,
            options.InputPath!);

        WriteSuccess(options, offsets);
        WriteFieldOfViewPatchStatus(fieldOfViewPatchStatus, fieldOfViewPatchApplied);
        console.MarkupLine($"Output: [white]{Markup.Escape(options.OutputPath!)}[/]");
        if (willCreateBackup)
        {
            console.MarkupLine($"Backup: [white]{Markup.Escape(backupPath!)}[/]");
        }

        var fieldOfViewIsPatched =
            fieldOfViewPatchStatus == FieldOfViewPatchStatus.AlreadyApplied || fieldOfViewPatchApplied;
        var fieldOfViewWarningWasShown = WriteFieldOfViewWarning(finalResolutions, fieldOfViewIsPatched);
        var steamWarningWasShown = WriteSteamWarning(options);
        WaitAfterWarnings(options, fieldOfViewWarningWasShown || steamWarningWasShown);

        return 0;
    }

    private bool ResolvePaths(PatchOptions options)
    {
        if (options.InputPath is null)
        {
            if (options.NonInteractive)
            {
                throw new UserInputException("--input is required in non-interactive mode.");
            }

            options.InputPath = fileDialogs.SelectInputFile();
            if (options.InputPath is null)
            {
                return false;
            }

            options.InputWasSelectedInteractively = true;
        }

        options.InputPath = NormalizePath(options.InputPath, "input");
        if (!File.Exists(options.InputPath))
        {
            throw new UserInputException($"The input file does not exist: {options.InputPath}");
        }

        if (options.OutputPath is null)
        {
            options.OutputPath = options.InputWasSelectedInteractively
                ? fileDialogs.SelectOutputFile(options.InputPath)
                : options.InputPath;

            if (options.OutputPath is null)
            {
                return false;
            }
        }

        options.OutputPath = NormalizePath(options.OutputPath, "output");
        var outputDirectory = Path.GetDirectoryName(options.OutputPath);
        if (outputDirectory is null || !Directory.Exists(outputDirectory))
        {
            throw new UserInputException($"The output directory does not exist: {outputDirectory ?? "<none>"}");
        }

        if (File.Exists(options.OutputPath) &&
            (File.GetAttributes(options.OutputPath) & FileAttributes.ReadOnly) != 0)
        {
            throw new UserInputException($"The output file is read-only: {options.OutputPath}");
        }

        return true;
    }

    private static void EnsureNonInteractiveOptionsAreComplete(PatchOptions options)
    {
        if (!options.NonInteractive)
        {
            return;
        }

        if (options.OldResolution is null)
        {
            throw new UserInputException(
                "--old-resolution, or both --old-width and --old-height, is required in non-interactive mode.");
        }

        if (options.NewResolution is null)
        {
            throw new UserInputException(
                "--new-resolution, or both --new-width and --new-height, is required in non-interactive mode.");
        }
    }

    private static string NormalizePath(string path, string displayName)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new UserInputException($"The {displayName} path is invalid: {exception.Message}");
        }
    }

    private static byte[] ReadInputFile(string inputPath)
    {
        try
        {
            var bytes = File.ReadAllBytes(inputPath);
            if (bytes.Length == 0)
            {
                throw new UserInputException($"The input file is empty: {inputPath}");
            }

            return bytes;
        }
        catch (UserInputException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new UserInputException($"The input file could not be read: {exception.Message}");
        }
    }

    private PatchMetadata? ReadMetadata(byte[] sourceBytes, out int executableLength)
    {
        try
        {
            return metadataCodec.Read(sourceBytes, out executableLength);
        }
        catch (InvalidDataException exception)
        {
            throw new UserInputException($"The executable contains invalid patch metadata: {exception.Message}");
        }
    }

    private static Resolution[] GetPatchableResolutions(
        IReadOnlyList<Resolution> gameResolutions,
        IReadOnlyDictionary<Resolution, IReadOnlyList<int>> offsets)
    {
        var patchable = gameResolutions
            .Distinct()
            .Where(resolution =>
                offsets.TryGetValue(resolution, out var matches) &&
                matches.Count == ResolutionPatternScanner.ExpectedMatchCount)
            .Order()
            .ToArray();

        if (patchable.Length == 0)
        {
            throw new UserInputException(
                $"No safe set of {ResolutionPatternScanner.ExpectedMatchCount} matching byte patterns " +
                "with a shared offset layout was found.");
        }

        return patchable;
    }

    private FieldOfViewPatchStatus AnalyzeFieldOfViewPatch(ReadOnlySpan<byte> executableBytes)
    {
        try
        {
            return fieldOfViewPatcher.Analyze(executableBytes);
        }
        catch (InvalidDataException exception)
        {
            throw new UserInputException($"The executable could not be analyzed for the FOV fix: {exception.Message}");
        }
    }

    private static void EnsureFinalResolutionsAreUnique(
        PatchOptions options,
        IReadOnlyList<Resolution> gameResolutions)
    {
        try
        {
            _ = options.ResolveFinalResolutions(gameResolutions);
        }
        catch (InvalidOperationException exception)
        {
            throw new UserInputException(exception.Message);
        }
    }

    private void ResolveOldResolution(
        PatchOptions options,
        IReadOnlyList<Resolution> gameResolutions,
        string backupStatus)
    {
        var available = gameResolutions.Distinct().Order().ToArray();
        if (options.OldResolution is { } oldResolution)
        {
            if (!available.Contains(oldResolution))
            {
                throw new UserInputException(
                    $"Resolution {oldResolution} is not available for replacement. " +
                    $"Available game resolutions: {FormatResolutions(available)}.");
            }

            return;
        }

        statusPresenter.Show(options, backupStatus);
        options.OldResolution = console.Prompt(
            new SelectionPrompt<Resolution>()
                .Title("Select the [deepskyblue1]game resolution to replace[/]:")
                .PageSize(Math.Min(15, Math.Max(3, available.Length)))
                .UseConverter(static resolution => resolution.ToString())
                .AddChoices(available));
    }

    private void ResolveNewResolution(
        PatchOptions options,
        IReadOnlyList<Resolution> gameResolutions,
        string backupStatus)
    {
        var oldResolution = options.OldResolution
            ?? throw new InvalidOperationException("The old resolution has not been resolved.");
        var unavailableTargets = options.GetUnavailableTargetResolutions(gameResolutions, oldResolution);

        if (options.IsUnchecked)
        {
            if (options.NewResolution is null)
            {
                statusPresenter.Show(options, backupStatus);
                options.NewResolution = PromptUncheckedResolution(
                    "Enter the [deepskyblue1]new width[/]:",
                    "Enter the [deepskyblue1]new height[/]:",
                    oldResolution,
                    unavailableTargets);
            }

            return;
        }

        var supported = GetSupportedResolutions();

        if (options.NewResolution is { } newResolution)
        {
            if (!supported.Contains(newResolution))
            {
                throw new UserInputException(
                    $"Resolution {newResolution} is not supported by Windows. " +
                    $"Supported resolutions: {FormatResolutions(supported)}. " +
                    "Use -u/--unchecked to bypass this check.");
            }

            return;
        }

        statusPresenter.Show(options, backupStatus);
        var choices = options.GetAvailableTargetResolutions(
            gameResolutions,
            oldResolution,
            supported);
        if (choices.Length == 0)
        {
            throw new UserInputException(
                $"Windows did not report an unused alternative resolution for {oldResolution}. " +
                "Use --unchecked to enter one manually.");
        }

        options.NewResolution = console.Prompt(
                new SelectionPrompt<Resolution>()
                    .Title("Select the [deepskyblue1]new resolution[/]:")
                    .PageSize(Math.Min(15, Math.Max(3, choices.Length)))
                    .UseConverter(FormatTargetResolution)
                    .AddChoices(choices));
    }

    private bool ResolveInteractiveReplacements(
        PatchOptions options,
        IReadOnlyList<Resolution> patchableResolutions,
        IReadOnlyList<Resolution> gameResolutions,
        string backupStatus)
    {
        var available = patchableResolutions.Distinct().Order().ToArray();
        var supported = options.IsUnchecked ? null : GetSupportedResolutions();
        string? menuMessage = null;

        while (true)
        {
            statusPresenter.Show(options, backupStatus);
            if (menuMessage is not null)
            {
                console.MarkupLine($"[yellow]{Markup.Escape(menuMessage)}[/]");
                menuMessage = null;
            }

            var choices = CreateResolutionMenuChoices(available, options.Replacements);
            var selected = console.Prompt(
                new SelectionPrompt<ResolutionMenuChoice>()
                    .Title("Select [deepskyblue1]resolution to change[/]:")
                    .PageSize(choices.Count)
                    .UseConverter(static choice => choice.Label)
                    .AddChoices(choices));

            switch (selected.Action)
            {
                case ResolutionMenuAction.Cancel:
                    return false;

                case ResolutionMenuAction.Patch when options.Replacements.Count > 0:
                    return true;

                case ResolutionMenuAction.Patch:
                    menuMessage = "Select at least one resolution before patching.";
                    continue;

                case ResolutionMenuAction.Change:
                    var oldResolution = selected.Resolution
                        ?? throw new InvalidOperationException("The selected resolution is missing.");
                    var newResolution = PromptReplacementResolution(
                        options,
                        oldResolution,
                        supported,
                        gameResolutions,
                        backupStatus);
                    options.SetReplacement(oldResolution, newResolution);
                    break;

                default:
                    throw new InvalidOperationException("The selected resolution action is not supported.");
            }
        }
    }

    private Resolution PromptReplacementResolution(
        PatchOptions options,
        Resolution oldResolution,
        Resolution[]? supported,
        IReadOnlyList<Resolution> gameResolutions,
        string backupStatus)
    {
        statusPresenter.Show(options, backupStatus);
        var unavailableTargets = options.GetUnavailableTargetResolutions(gameResolutions, oldResolution);

        if (!options.IsUnchecked)
        {
            var choices = options.GetAvailableTargetResolutions(
                gameResolutions,
                oldResolution,
                supported!);
            if (choices.Length == 0)
            {
                throw new UserInputException(
                    $"Windows did not report an unused alternative resolution for {oldResolution}. " +
                    "Use --unchecked to enter one manually.");
            }

            return console.Prompt(
                new SelectionPrompt<Resolution>()
                    .Title($"Select the [deepskyblue1]new resolution for {oldResolution}[/]:")
                    .PageSize(Math.Min(15, Math.Max(3, choices.Length)))
                    .UseConverter(FormatTargetResolution)
                    .AddChoices(choices));
        }

        return PromptUncheckedResolution(
            $"Enter the [deepskyblue1]new width for {oldResolution}[/]:",
            $"Enter the [deepskyblue1]new height for {oldResolution}[/]:",
            oldResolution,
            unavailableTargets);
    }

    private Resolution[] GetSupportedResolutions()
    {
        var supported = supportedResolutionProvider.GetSupportedResolutions()
            .Distinct()
            .Order()
            .ToArray();
        if (supported.Length == 0)
        {
            throw new UserInputException(
                "Windows did not report any supported display resolutions. Use --unchecked to bypass this check.");
        }

        return supported;
    }

    private List<ResolutionMenuChoice> CreateResolutionMenuChoices(
        Resolution[] available,
        IReadOnlyList<ResolutionReplacement> replacements)
    {
        var choices = available
            .Select((resolution, index) =>
            {
                var replacement = replacements.FirstOrDefault(
                    candidate => candidate.OldResolution == resolution);
                var mapping = replacement is null
                    ? resolution.ToString()
                    : $"{resolution} [green]-> {FormatTargetResolution(replacement.NewResolution)}[/]";

                return new ResolutionMenuChoice(
                    ResolutionMenuAction.Change,
                    resolution,
                    $"{index + 1}. {mapping}");
            })
            .ToList();

        var patchNumber = available.Length + 1;
        var patchLabel = replacements.Count > 0
            ? $"{patchNumber}. [green]Patch[/]"
            : $"{patchNumber}. [grey]Patch (select at least one resolution)[/]";
        choices.Add(new ResolutionMenuChoice(ResolutionMenuAction.Patch, null, patchLabel));
        choices.Add(new ResolutionMenuChoice(
            ResolutionMenuAction.Cancel,
            null,
            $"{patchNumber + 1}. [yellow]Cancel[/]"));

        return choices;
    }

    private bool TryApplyFieldOfViewPatch(
        PatchOptions options,
        IReadOnlyList<Resolution> finalResolutions,
        FieldOfViewPatchStatus patchStatus,
        ref byte[] patchedExecutable)
    {
        if (patchStatus == FieldOfViewPatchStatus.AlreadyApplied)
        {
            return false;
        }

        if (options.ApplyFieldOfViewFix)
        {
            if (patchStatus != FieldOfViewPatchStatus.Available)
            {
                throw new UserInputException(
                    "The automatic FOV fix was requested, but the supported SetFOV function " +
                    $"was not found at physical file offset 0x{FieldOfViewPatcher.OriginalFunctionOffset:X5}.");
            }

            patchedExecutable = ApplyFieldOfViewPatch(patchedExecutable);
            return true;
        }

        if (options.NonInteractive ||
            patchStatus != FieldOfViewPatchStatus.Available ||
            finalResolutions.All(IsFourByThree))
        {
            return false;
        }

        console.WriteLine();
        console.MarkupLine("[deepskyblue1]Automatic FOV fix available[/]");
        console.WriteLine(
            "Zanzarah is designed for a 4:3 aspect ratio and uses the default FOV 1000,750. " +
            "Keeping that FOV at a non-4:3 resolution distorts the image.");
        console.WriteLine(
            "The automatic fix makes the game calculate the horizontal FOV from the active screen size. " +
            "If you decline, you must set the correct FOV through the console after every game launch.");

        if (!console.Confirm("Apply the automatic FOV fix?", defaultValue: true))
        {
            return false;
        }

        patchedExecutable = ApplyFieldOfViewPatch(patchedExecutable);
        return true;
    }

    private byte[] ApplyFieldOfViewPatch(ReadOnlySpan<byte> executableBytes)
    {
        try
        {
            return fieldOfViewPatcher.Apply(executableBytes);
        }
        catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException)
        {
            throw new UserInputException($"The automatic FOV fix could not be applied: {exception.Message}");
        }
    }

    private void WriteFieldOfViewPatchStatus(
        FieldOfViewPatchStatus patchStatus,
        bool patchWasApplied)
    {
        if (patchWasApplied)
        {
            console.MarkupLine("[green]FOV fix:[/] automatic calculation was enabled.");
        }
        else if (patchStatus == FieldOfViewPatchStatus.AlreadyApplied)
        {
            console.MarkupLine("[green]FOV fix:[/] automatic calculation was already enabled.");
        }
    }

    private void WriteSuccess(
        PatchOptions options,
        IReadOnlyDictionary<Resolution, IReadOnlyList<int>> offsets)
    {
        var patternCount = options.Replacements.Sum(
            replacement => offsets[replacement.OldResolution].Count);

        console.MarkupLine(
            $"[green]Success:[/] applied [white]{options.Replacements.Count}[/] resolution " +
            $"replacement(s) across [white]{patternCount}[/] pattern(s).");

        foreach (var replacement in options.Replacements)
        {
            console.MarkupLine(
                $"  [white]{Markup.Escape(replacement.OldResolution.ToString())} -> " +
                $"{Markup.Escape(FormatTargetResolution(replacement.NewResolution))}[/]");
        }
    }

    private bool WriteFieldOfViewWarning(
        IReadOnlyList<Resolution> finalResolutions,
        bool fieldOfViewIsPatched)
    {
        ArgumentNullException.ThrowIfNull(finalResolutions);

        if (fieldOfViewIsPatched)
        {
            return false;
        }

        var widescreenTargets = finalResolutions
            .Where(static resolution => !IsFourByThree(resolution))
            .Distinct()
            .Order()
            .ToArray();
        if (widescreenTargets.Length == 0)
        {
            return false;
        }

        console.WriteLine();
        console.Write(new FigletText("FOV REQUIRED").Color(Spectre.Console.Color.Red));
        console.WriteLine(
            "Zanzarah does not preserve the corrected field of view. " +
            "Without it, non-4:3 gameplay is visibly distorted.");
        console.WriteLine(
            "Enable the game console, then enter the matching command after every game launch:");

        foreach (var resolution in widescreenTargets)
        {
            console.WriteLine(
                $"  {resolution}: {fieldOfViewCalculator.Calculate(resolution)}");
        }

        console.WriteLine(
            "Edit ZanZarah.bat to use start zanzarah.exe -console, start the game, " +
            "select the patched resolution, press F11, and enter the command.");

        return true;
    }

    private void WaitAfterWarnings(
        PatchOptions options,
        bool warningWasShown)
    {
        if (options.NonInteractive || !warningWasShown)
        {
            return;
        }

        console.Prompt(
            new TextPrompt<string>("[grey]Press ENTER to exit...[/]")
                .AllowEmpty());
    }

    private string FormatTargetResolution(Resolution resolution) =>
        $"{resolution} ({fieldOfViewCalculator.Calculate(resolution)})";

    private static bool IsFourByThree(Resolution resolution) =>
        (long)resolution.Width * 3 == (long)resolution.Height * 4;

    private bool WriteSteamWarning(PatchOptions options)
    {
        if (!GamePathDetector.IsSteamInstallation(options.InputPath!))
        {
            return false;
        }

        const string issueUrl =
            "https://steamcommunity.com/app/384570/discussions/1/521643320368729405/";

        console.WriteLine();
        console.Write(new FigletText("STEAM WARNING").Color(Spectre.Console.Color.Red));
        console.WriteLine(
            "The Steam version has a known issue: Alt+Tab or other task switching may " +
            "crash or reload the game, especially in windowed mode.");
        console.WriteLine(
            "This is a game issue and is not caused by the resolution patch. " +
            "Avoid Alt+Tab while playing. If you are reading this in the future and the issue is fixed, " +
            "ignore this warning.");
        console.WriteLine($"Details: {issueUrl}");
        return true;
    }

    private Resolution PromptUncheckedResolution(
        string widthPrompt,
        string heightPrompt,
        Resolution oldResolution,
        IReadOnlyCollection<Resolution> unavailableTargets)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(widthPrompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(heightPrompt);
        ArgumentNullException.ThrowIfNull(unavailableTargets);

        while (true)
        {
            var width = PromptDimension(widthPrompt);
            var height = PromptDimension(heightPrompt);
            var resolution = new Resolution(width, height);

            if (resolution == oldResolution)
            {
                console.MarkupLine("[yellow]The replacement resolution must be different.[/]");
                continue;
            }

            if (unavailableTargets.Contains(resolution))
            {
                console.MarkupLine(
                    $"[yellow]Resolution {Markup.Escape(resolution.ToString())} is already used " +
                    "by another game resolution. Choose a unique resolution.[/]");
                continue;
            }

            return resolution;
        }
    }

    private ushort PromptDimension(string prompt)
    {
        return console.Prompt(
            new TextPrompt<ushort>(prompt)
                .ValidationErrorMessage("[red]Enter an integer from 1 to 65535.[/]")
                .Validate(static value => value > 0));
    }

    private static (string? Path, bool Create, string Status) ResolveBackup(
        PatchOptions options,
        PatchMetadata? metadata)
    {
        var isInPlace = string.Equals(
            options.InputPath,
            options.OutputPath,
            StringComparison.OrdinalIgnoreCase);

        if (!isInPlace)
        {
            return (null, false, "Not needed (separate output file)");
        }

        var backupPath = options.InputPath + "_resolution.bak";
        if (options.NoBackup)
        {
            return (backupPath, false, $"Disabled ({backupPath})");
        }

        if (metadata is not null)
        {
            return (backupPath, false, $"Not needed (patch metadata exists; {backupPath})");
        }

        if (File.Exists(backupPath))
        {
            return (backupPath, false, $"Already exists ({backupPath})");
        }

        return (backupPath, true, $"Will be created ({backupPath})");
    }

    private static string FormatResolutions(IEnumerable<Resolution> resolutions) =>
        string.Join(", ", resolutions.Select(static resolution => resolution.ToString()));

    private sealed record ResolutionMenuChoice(
        ResolutionMenuAction Action,
        Resolution? Resolution,
        string Label);

    private enum ResolutionMenuAction
    {
        Change,
        Patch,
        Cancel,
    }
}
