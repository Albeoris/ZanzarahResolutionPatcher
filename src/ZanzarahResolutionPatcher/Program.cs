using System.Reflection;
using System.Globalization;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Windows.Forms;
using ZanzarahResolutionPatcher.Cli;
using ZanzarahResolutionPatcher.Infrastructure;

namespace ZanzarahResolutionPatcher;

public static class Program
{
    private static readonly Dictionary<string, string> MultiCharacterAliases =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["-or"] = "--old-resolution",
            ["-ow"] = "--old-width",
            ["-oh"] = "--old-height",
            ["-nr"] = "--new-resolution",
            ["-nw"] = "--new-width",
            ["-nh"] = "--new-height",
            ["-nb"] = "--no-backup",
            ["-ni"] = "--non-interactive",
        };

    [STAThread]
    public static int Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();

        var normalizedArguments = NormalizeArguments(args);
        var nonInteractive = normalizedArguments.Any(
            argument => argument.Equals("-ni", StringComparison.OrdinalIgnoreCase) ||
                        argument.Equals("--non-interactive", StringComparison.OrdinalIgnoreCase));

        var app = new CommandApp<PatchCommand>();
        app.Configure(configuration =>
        {
            configuration.SetApplicationName("ZanzarahResolutionPatcher");
            configuration.SetApplicationCulture(CultureInfo.GetCultureInfo("en-US"));
            configuration.SetApplicationVersion(
                Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0");
            configuration.PropagateExceptions();
            configuration.AddExample(
                "--input", @"C:\Games\ZanZarah\System\zanthp.exe",
                "--old-resolution", "800x600",
                "--new-resolution", "1920x1080");
        });

        try
        {
            return app.Run(normalizedArguments);
        }
        catch (UserInputException exception)
        {
            return ExitWithError(exception.Message, 1, nonInteractive);
        }
        catch (CommandParseException exception)
        {
            return ExitWithError(exception.Message, 1, nonInteractive);
        }
        catch (Exception exception)
        {
            return ExitWithError(exception.Message, 2, nonInteractive);
        }
    }

    private static string[] NormalizeArguments(string[] arguments)
    {
        var normalized = new List<string>(arguments.Length);

        for (var index = 0; index < arguments.Length; index++)
        {
            var argument = arguments[index];
            if (MultiCharacterAliases.TryGetValue(argument, out var expandedAlias))
            {
                normalized.Add(expandedAlias);
                continue;
            }

            if (argument.Equals("/?", StringComparison.OrdinalIgnoreCase))
            {
                normalized.Add("--help");
                continue;
            }

            normalized.Add(argument);
        }

        return [.. normalized];
    }

    private static int ExitWithError(string message, int exitCode, bool nonInteractive)
    {
        AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(message)}");
        if (!nonInteractive)
        {
            AnsiConsole.MarkupLine("[grey]Press ENTER to exit...[/]");
            Console.ReadLine();
        }

        return exitCode;
    }
}
