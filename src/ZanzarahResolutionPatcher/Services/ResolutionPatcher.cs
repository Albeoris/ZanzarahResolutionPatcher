using System.Buffers.Binary;
using ZanzarahResolutionPatcher.Domain;

namespace ZanzarahResolutionPatcher.Services;

public sealed class ResolutionPatcher(PatchMetadataCodec metadataCodec)
{
    public byte[] Patch(PatchPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (plan.Options.Replacements.Count == 0)
        {
            throw new InvalidOperationException("No resolution replacements have been resolved.");
        }

        var executable = plan.SourceBytes.AsSpan(0, plan.ExecutableLength).ToArray();
        foreach (var replacement in plan.Options.Replacements)
        {
            if (!plan.PatternOffsets.TryGetValue(replacement.OldResolution, out var offsets) || offsets.Count == 0)
            {
                throw new InvalidDataException(
                    $"No {replacement.OldResolution} patterns were found in the executable.");
            }

            foreach (var offset in offsets)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(
                    executable.AsSpan(offset), replacement.NewResolution.Width);
                BinaryPrimitives.WriteUInt16LittleEndian(executable.AsSpan(offset + sizeof(ushort)), 0);
                BinaryPrimitives.WriteUInt16LittleEndian(
                    executable.AsSpan(offset + (sizeof(ushort) * 2)), replacement.NewResolution.Height);
            }
        }

        var replacements = plan.Options.Replacements.ToDictionary(
            static replacement => replacement.OldResolution,
            static replacement => replacement.NewResolution);
        var updatedResolutions = plan.GameResolutions
            .Select(resolution => replacements.GetValueOrDefault(resolution, resolution))
            .ToArray();

        return metadataCodec.Append(executable, updatedResolutions);
    }
}
