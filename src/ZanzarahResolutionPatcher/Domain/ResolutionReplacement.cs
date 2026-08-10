namespace ZanzarahResolutionPatcher.Domain;

public sealed record ResolutionReplacement(
    Resolution OldResolution,
    Resolution NewResolution)
{
    public override string ToString() => $"{OldResolution} -> {NewResolution}";
}
