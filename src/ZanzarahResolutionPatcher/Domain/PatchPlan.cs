namespace ZanzarahResolutionPatcher.Domain;

public sealed record PatchPlan(
    PatchOptions Options,
    byte[] SourceBytes,
    int ExecutableLength,
    PatchMetadata? ExistingMetadata,
    IReadOnlyList<Resolution> GameResolutions,
    IReadOnlyDictionary<Resolution, IReadOnlyList<int>> PatternOffsets,
    string? BackupPath,
    bool WillCreateBackup);
