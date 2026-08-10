using Spectre.Console;
using ZanzarahResolutionPatcher.Domain;

namespace ZanzarahResolutionPatcher.Services;

public sealed class StatusPresenter(
    IAnsiConsole console,
    FieldOfViewCalculator fieldOfViewCalculator)
{
    public void Show(PatchOptions options, string backupStatus)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!Console.IsOutputRedirected)
        {
            console.Clear();
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold]Zanzarah Resolution Patcher[/]")
            .HideHeaders()
            .AddColumn("Parameter")
            .AddColumn("Value");

        AddRow(table, "Input", options.InputPath);
        AddRow(table, "Output", options.OutputPath);
        if (options.Replacements.Count == 0)
        {
            AddRow(table, "Old resolution", options.OldResolution?.ToString());
            AddRow(
                table,
                "New resolution",
                options.NewResolution is { } resolution
                    ? FormatTargetResolution(resolution)
                    : null);
        }
        else
        {
            for (var index = 0; index < options.Replacements.Count; index++)
            {
                AddRow(
                    table,
                    index == 0 ? "Replacements" : string.Empty,
                    FormatReplacement(options.Replacements[index]));
            }
        }
        AddRow(table, "Backup", backupStatus);
        AddRow(table, "Validation", options.IsUnchecked ? "Unchecked" : "Windows display modes");
        AddRow(table, "Interaction", options.NonInteractive ? "Disabled" : "Enabled");

        console.Write(table);
        console.WriteLine();
    }

    private static void AddRow(Table table, string label, string? value)
    {
        table.AddRow(
            $"[deepskyblue1]{Markup.Escape(label)}[/]",
            value is null
                ? "[grey]<not set>[/]"
                : $"[white]{Markup.Escape(value)}[/]");
    }

    private string FormatReplacement(ResolutionReplacement replacement) =>
        $"{replacement.OldResolution} -> {FormatTargetResolution(replacement.NewResolution)}";

    private string FormatTargetResolution(Resolution resolution) =>
        $"{resolution} ({fieldOfViewCalculator.Calculate(resolution)})";
}
