using System.Buffers.Binary;
using ZanzarahResolutionPatcher.Domain;

namespace ZanzarahResolutionPatcher.Services;

public sealed class ResolutionPatternScanner
{
    public IReadOnlyDictionary<Resolution, IReadOnlyList<int>> FindAll(
        ReadOnlySpan<byte> executableBytes,
        IEnumerable<Resolution> resolutions)
    {
        ArgumentNullException.ThrowIfNull(resolutions);

        var offsets = resolutions
            .Distinct()
            .ToDictionary(static resolution => resolution, static _ => new List<int>());

        for (var offset = 0; offset <= executableBytes.Length - Resolution.BinarySize;)
        {
            if (BinaryPrimitives.ReadUInt16LittleEndian(executableBytes[(offset + sizeof(ushort))..]) == 0)
            {
                var candidate = new Resolution(
                    BinaryPrimitives.ReadUInt16LittleEndian(executableBytes[offset..]),
                    BinaryPrimitives.ReadUInt16LittleEndian(
                        executableBytes[(offset + (sizeof(ushort) * 2))..]));

                if (offsets.TryGetValue(candidate, out var matches))
                {
                    matches.Add(offset);
                    offset += Resolution.BinarySize;
                    continue;
                }
            }

            offset++;
        }

        return offsets.ToDictionary(
            static pair => pair.Key,
            static pair => (IReadOnlyList<int>)pair.Value.AsReadOnly());
    }
}
