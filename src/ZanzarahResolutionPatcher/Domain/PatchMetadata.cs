namespace ZanzarahResolutionPatcher.Domain;

public sealed record PatchMetadata(IReadOnlyList<Resolution> Resolutions, uint Version);
