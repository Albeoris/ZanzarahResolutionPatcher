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
}
