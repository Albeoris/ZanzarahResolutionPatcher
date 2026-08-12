using System.ComponentModel;
using Spectre.Console.Cli;

namespace ZanzarahResolutionPatcher.Cli;

public sealed class PatchCommandSettings : CommandSettings
{
    [CommandOption("-i|--input <PATH>")]
    [Description("Path to the Zanzarah executable.")]
    public string? InputPath { get; init; }

    [CommandOption("-o|--output <PATH>")]
    [Description("Output path. Defaults to the input path unless the input was selected in a dialog.")]
    public string? OutputPath { get; init; }

    [CommandOption("--old-resolution <WIDTHxHEIGHT>")]
    [Description("Game resolution to replace, for example 800x600. Alias: -or.")]
    public string? OldResolution { get; init; }

    [CommandOption("--new-resolution <WIDTHxHEIGHT>")]
    [Description("New resolution, for example 1920x1080. Alias: -nr.")]
    public string? NewResolution { get; init; }

    [CommandOption("--old-width <WIDTH>")]
    [Description("Width of the game resolution to replace. Use with --old-height. Alias: -ow.")]
    public string? OldWidth { get; init; }

    [CommandOption("--old-height <HEIGHT>")]
    [Description("Height of the game resolution to replace. Use with --old-width. Alias: -oh.")]
    public string? OldHeight { get; init; }

    [CommandOption("--new-width <WIDTH>")]
    [Description("New width. Use with --new-height. Alias: -nw.")]
    public string? NewWidth { get; init; }

    [CommandOption("--new-height <HEIGHT>")]
    [Description("New height. Use with --new-width. Alias: -nh.")]
    public string? NewHeight { get; init; }

    [CommandOption("-u|--unchecked")]
    [Description("Allow a new resolution that is not reported by Windows.")]
    public bool IsUnchecked { get; init; }

    [CommandOption("--no-backup")]
    [Description("Do not create an in-place backup of an unpatched executable. Alias: -nb.")]
    public bool NoBackup { get; init; }

    [CommandOption("--non-interactive")]
    [Description("Disable dialogs, prompts, confirmation, and the error pause. Alias: -ni.")]
    public bool NonInteractive { get; init; }

    [CommandOption("--fov-fix")]
    [Description("Apply the automatic FOV fix without prompting; fail if it is unavailable.")]
    public bool FovFix { get; init; }
}
