using System.Buffers.Binary;
using ZanzarahResolutionPatcher.Domain;

namespace ZanzarahResolutionPatcher.Services;

public sealed class ResolutionPatternScanner
{
    public const int ExpectedMatchCount = 6;

    private const int MaximumCandidateCount = 256;

    public IReadOnlyDictionary<Resolution, IReadOnlyList<int>> FindAll(
        ReadOnlySpan<byte> executableBytes,
        IEnumerable<Resolution> resolutions)
    {
        ArgumentNullException.ThrowIfNull(resolutions);

        var rawOffsets = resolutions
            .Distinct()
            .ToDictionary(static resolution => resolution, static _ => new List<int>());

        Scan(executableBytes, rawOffsets);
        return FindSharedLayout(rawOffsets);
    }

    private static void Scan(
        ReadOnlySpan<byte> executableBytes,
        Dictionary<Resolution, List<int>> rawOffsets)
    {
        for (var offset = 0; offset <= executableBytes.Length - Resolution.BinarySize;)
        {
            if (BinaryPrimitives.ReadUInt16LittleEndian(executableBytes[(offset + sizeof(ushort))..]) == 0)
            {
                var candidate = new Resolution(
                    BinaryPrimitives.ReadUInt16LittleEndian(executableBytes[offset..]),
                    BinaryPrimitives.ReadUInt16LittleEndian(
                        executableBytes[(offset + (sizeof(ushort) * 2))..]));

                if (rawOffsets.TryGetValue(candidate, out var matches))
                {
                    if (matches.Count <= MaximumCandidateCount)
                    {
                        matches.Add(offset);
                    }

                    offset += Resolution.BinarySize;
                    continue;
                }
            }

            offset++;
        }
    }

    private static Dictionary<Resolution, IReadOnlyList<int>> FindSharedLayout(
        Dictionary<Resolution, List<int>> rawOffsets)
    {
        var eligible = rawOffsets
            .Where(static pair =>
                pair.Value.Count >= ExpectedMatchCount &&
                pair.Value.Count <= MaximumCandidateCount)
            .ToArray();
        var layouts = new Dictionary<OffsetLayout, LayoutCandidate>();

        for (var leftIndex = 0; leftIndex < eligible.Length - 1; leftIndex++)
        {
            for (var rightIndex = leftIndex + 1; rightIndex < eligible.Length; rightIndex++)
            {
                RegisterSharedLayouts(eligible[leftIndex], eligible[rightIndex], layouts);
            }
        }

        var rankedLayouts = layouts.Values
            .Where(static candidate => !candidate.IsAmbiguous && candidate.MatchCount >= 2)
            .OrderByDescending(static candidate => candidate.MatchCount)
            .ToArray();
        if (rankedLayouts.Length == 0 ||
            (rankedLayouts.Length > 1 && rankedLayouts[0].MatchCount == rankedLayouts[1].MatchCount))
        {
            return new Dictionary<Resolution, IReadOnlyList<int>>();
        }

        return rankedLayouts[0].Matches.ToDictionary(
            static pair => pair.Key,
            static pair => (IReadOnlyList<int>)pair.Value.AsReadOnly());
    }

    private static void RegisterSharedLayouts(
        KeyValuePair<Resolution, List<int>> left,
        KeyValuePair<Resolution, List<int>> right,
        IDictionary<OffsetLayout, LayoutCandidate> layouts)
    {
        var matchesByTranslation = new Dictionary<int, List<(int Left, int Right)>>();
        foreach (var leftOffset in left.Value)
        {
            foreach (var rightOffset in right.Value)
            {
                var translation = rightOffset - leftOffset;
                if (!matchesByTranslation.TryGetValue(translation, out var translatedMatches))
                {
                    translatedMatches = [];
                    matchesByTranslation.Add(translation, translatedMatches);
                }

                translatedMatches.Add((leftOffset, rightOffset));
            }
        }

        foreach (var translatedMatches in matchesByTranslation.Values
                     .Where(static matches => matches.Count == ExpectedMatchCount))
        {
            var leftMatches = translatedMatches.Select(static match => match.Left).ToArray();
            var rightMatches = translatedMatches.Select(static match => match.Right).ToArray();
            var layout = new OffsetLayout(leftMatches);

            if (!layouts.TryGetValue(layout, out var candidate))
            {
                candidate = new LayoutCandidate();
                layouts.Add(layout, candidate);
            }

            candidate.Add(left.Key, leftMatches);
            candidate.Add(right.Key, rightMatches);
        }
    }

    private sealed class LayoutCandidate
    {
        private readonly Dictionary<Resolution, List<int>> matches = [];

        public bool IsAmbiguous { get; private set; }

        public int MatchCount => matches.Count;

        public IReadOnlyDictionary<Resolution, List<int>> Matches => matches;

        public void Add(Resolution resolution, IReadOnlyList<int> offsets)
        {
            if (matches.TryGetValue(resolution, out var existing))
            {
                IsAmbiguous |= !existing.SequenceEqual(offsets);
                return;
            }

            matches.Add(resolution, [.. offsets]);
        }
    }

    private sealed class OffsetLayout : IEquatable<OffsetLayout>
    {
        private readonly int[] relativeOffsets;

        public OffsetLayout(IReadOnlyList<int> offsets)
        {
            if (offsets.Count != ExpectedMatchCount)
            {
                throw new ArgumentException(
                    $"Exactly {ExpectedMatchCount} offsets are required.", nameof(offsets));
            }

            relativeOffsets = offsets
                .Select(offset => offset - offsets[0])
                .ToArray();
        }

        public bool Equals(OffsetLayout? other) =>
            other is not null && relativeOffsets.SequenceEqual(other.relativeOffsets);

        public override bool Equals(object? obj) => Equals(obj as OffsetLayout);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (var offset in relativeOffsets)
            {
                hash.Add(offset);
            }

            return hash.ToHashCode();
        }
    }
}
