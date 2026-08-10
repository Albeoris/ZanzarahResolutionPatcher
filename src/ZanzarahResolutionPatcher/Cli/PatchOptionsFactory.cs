using System.Globalization;
using ZanzarahResolutionPatcher.Domain;
using ZanzarahResolutionPatcher.Infrastructure;

namespace ZanzarahResolutionPatcher.Cli;

public sealed class PatchOptionsFactory
{
    public PatchOptions Create(PatchCommandSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new PatchOptions
        {
            InputPath = CleanPath(settings.InputPath),
            OutputPath = CleanPath(settings.OutputPath),
            OldResolution = ParseResolution(
                settings.OldResolution,
                settings.OldWidth,
                settings.OldHeight,
                "old resolution",
                "--old-resolution",
                "--old-width",
                "--old-height"),
            NewResolution = ParseResolution(
                settings.NewResolution,
                settings.NewWidth,
                settings.NewHeight,
                "new resolution",
                "--new-resolution",
                "--new-width",
                "--new-height"),
            IsUnchecked = settings.IsUnchecked,
            NoBackup = settings.NoBackup,
            NonInteractive = settings.NonInteractive,
        };
    }

    private static string? CleanPath(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().Trim('"');

    private static Resolution? ParseResolution(
        string? combined,
        string? width,
        string? height,
        string displayName,
        string combinedOption,
        string widthOption,
        string heightOption)
    {
        var hasCombined = !string.IsNullOrWhiteSpace(combined);
        var hasWidth = !string.IsNullOrWhiteSpace(width);
        var hasHeight = !string.IsNullOrWhiteSpace(height);

        if (hasCombined && (hasWidth || hasHeight))
        {
            throw new UserInputException(
                $"Specify the {displayName} either with {combinedOption} or with " +
                $"{widthOption} and {heightOption}, not both.");
        }

        if (hasWidth != hasHeight)
        {
            var missingOption = hasWidth ? heightOption : widthOption;
            throw new UserInputException($"{missingOption} is required to complete the {displayName}.");
        }

        if (hasCombined)
        {
            if (!Resolution.TryParse(combined, out var resolution))
            {
                throw new UserInputException(
                    $"The {displayName} '{combined}' is invalid. Use WIDTHxHEIGHT with values from 1 to 65535.");
            }

            return resolution;
        }

        if (!hasWidth)
        {
            return null;
        }

        return new Resolution(
            ParseDimension(width!, widthOption),
            ParseDimension(height!, heightOption));
    }

    private static ushort ParseDimension(string value, string optionName)
    {
        if (!ushort.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result) || result == 0)
        {
            throw new UserInputException(
                $"The value '{value}' for {optionName} is invalid. Use an integer from 1 to 65535.");
        }

        return result;
    }
}
