using ZanzarahResolutionPatcher.Domain;

namespace ZanzarahResolutionPatcher.Services;

public sealed class FieldOfViewCalculator
{
    private const int ReferenceVertical = 750;
    private const int HorizontalStep = 50;

    public GameFieldOfView Calculate(Resolution resolution)
    {
        var exactHorizontal = (double)resolution.Width / resolution.Height * ReferenceVertical;
        var roundedHorizontal = (int)Math.Round(
            exactHorizontal / HorizontalStep,
            MidpointRounding.AwayFromZero) * HorizontalStep;

        return new GameFieldOfView(Math.Max(HorizontalStep, roundedHorizontal), ReferenceVertical);
    }

    public double ConvertHorizontalAngle(
        double degrees,
        double sourceAspectRatio,
        double targetAspectRatio)
    {
        if (degrees is <= 0 or >= 180)
        {
            throw new ArgumentOutOfRangeException(nameof(degrees), "FOV must be between 0 and 180 degrees.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceAspectRatio);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetAspectRatio);

        var radians = degrees * Math.PI / 180;
        var convertedRadians = 2 * Math.Atan(
            Math.Tan(radians / 2) / sourceAspectRatio * targetAspectRatio);

        return convertedRadians * 180 / Math.PI;
    }
}
