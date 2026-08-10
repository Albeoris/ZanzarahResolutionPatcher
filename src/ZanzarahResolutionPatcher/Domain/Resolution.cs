using System.Globalization;

namespace ZanzarahResolutionPatcher.Domain;

public readonly record struct Resolution(ushort Width, ushort Height) : IComparable<Resolution>
{
    public const int BinarySize = sizeof(ushort) * 3;

    public int CompareTo(Resolution other)
    {
        var widthComparison = Width.CompareTo(other.Width);
        return widthComparison != 0 ? widthComparison : Height.CompareTo(other.Height);
    }

    public override string ToString() => $"{Width}x{Height}";

    public static bool operator <(Resolution left, Resolution right) => left.CompareTo(right) < 0;

    public static bool operator <=(Resolution left, Resolution right) => left.CompareTo(right) <= 0;

    public static bool operator >(Resolution left, Resolution right) => left.CompareTo(right) > 0;

    public static bool operator >=(Resolution left, Resolution right) => left.CompareTo(right) >= 0;

    public static bool TryParse(string? value, out Resolution resolution)
    {
        resolution = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split('x', StringSplitOptions.TrimEntries);
        if (parts.Length != 2 ||
            !ushort.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var width) ||
            !ushort.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var height) ||
            width == 0 ||
            height == 0)
        {
            return false;
        }

        resolution = new Resolution(width, height);
        return true;
    }
}
