namespace ZanzarahResolutionPatcher.Domain;

public sealed class PatchOptions
{
    private readonly List<ResolutionReplacement> replacements = [];

    public string? InputPath { get; set; }

    public string? OutputPath { get; set; }

    public Resolution? OldResolution { get; set; }

    public Resolution? NewResolution { get; set; }

    public bool IsUnchecked { get; init; }

    public bool NoBackup { get; init; }

    public bool NonInteractive { get; init; }

    public bool ApplyFieldOfViewFix { get; init; }

    public bool InputWasSelectedInteractively { get; set; }

    public IReadOnlyList<ResolutionReplacement> Replacements => replacements;

    public void SetReplacement(Resolution oldResolution, Resolution newResolution)
    {
        if (oldResolution == newResolution)
        {
            throw new ArgumentException("The old and new resolutions must be different.", nameof(newResolution));
        }

        var index = replacements.FindIndex(replacement => replacement.OldResolution == oldResolution);
        var replacement = new ResolutionReplacement(oldResolution, newResolution);

        if (index >= 0)
        {
            replacements[index] = replacement;
        }
        else
        {
            replacements.Add(replacement);
        }
    }

    public Resolution[] ResolveFinalResolutions(IReadOnlyList<Resolution> gameResolutions)
    {
        ArgumentNullException.ThrowIfNull(gameResolutions);

        var replacementsByResolution = replacements.ToDictionary(
            static replacement => replacement.OldResolution,
            static replacement => replacement.NewResolution);
        var finalResolutions = gameResolutions
            .Select(resolution => replacementsByResolution.GetValueOrDefault(resolution, resolution))
            .ToArray();
        var duplicates = finalResolutions
            .GroupBy(static resolution => resolution)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .Order()
            .ToArray();

        if (duplicates.Length > 0)
        {
            throw new InvalidOperationException(
                $"The patch would create duplicate game resolution(s): {string.Join(", ", duplicates)}. " +
                "Every game resolution must remain unique.");
        }

        return finalResolutions;
    }

    public Resolution[] GetUnavailableTargetResolutions(
        IReadOnlyList<Resolution> gameResolutions,
        Resolution oldResolution)
    {
        ArgumentNullException.ThrowIfNull(gameResolutions);

        if (!gameResolutions.Contains(oldResolution))
        {
            throw new ArgumentException(
                $"Resolution {oldResolution} is not present in the game resolution list.",
                nameof(oldResolution));
        }

        var replacementsByResolution = replacements.ToDictionary(
            static replacement => replacement.OldResolution,
            static replacement => replacement.NewResolution);

        return gameResolutions
            .Where(resolution => resolution != oldResolution)
            .Select(resolution => replacementsByResolution.GetValueOrDefault(resolution, resolution))
            .Distinct()
            .Order()
            .ToArray();
    }

    public Resolution[] GetAvailableTargetResolutions(
        IReadOnlyList<Resolution> gameResolutions,
        Resolution oldResolution,
        IEnumerable<Resolution> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        var unavailableTargets = GetUnavailableTargetResolutions(gameResolutions, oldResolution);

        return candidates
            .Where(resolution =>
                resolution != oldResolution &&
                !unavailableTargets.Contains(resolution))
            .Distinct()
            .ToArray();
    }
}
