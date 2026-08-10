using ZanzarahResolutionPatcher.Domain;
using ZanzarahResolutionPatcher.Services;

namespace ZanzarahResolutionPatcher.Tests;

public sealed class FieldOfViewCalculatorTests
{
    private readonly FieldOfViewCalculator calculator = new();

    [Theory]
    [InlineData(800, 600, 1000, 750)]
    [InlineData(1920, 1080, 1350, 750)]
    [InlineData(2560, 1440, 1350, 750)]
    [InlineData(1920, 1200, 1200, 750)]
    [InlineData(3440, 1440, 1800, 750)]
    public void Calculate_UsesAspectRatioAndMatchesRecommendedGameValues(
        ushort width,
        ushort height,
        int expectedHorizontal,
        int expectedVertical)
    {
        var result = calculator.Calculate(new Resolution(width, height));

        Assert.Equal(new GameFieldOfView(expectedHorizontal, expectedVertical), result);
    }

    [Fact]
    public void ConvertHorizontalAngle_FromFourByThreeThroughSixteenByNineToSixteenByTen_IsConsistent()
    {
        var sixteenByNine = calculator.ConvertHorizontalAngle(45, 4d / 3, 16d / 9);
        var sixteenByTen = calculator.ConvertHorizontalAngle(sixteenByNine, 16d / 9, 16d / 10);

        Assert.Equal(57.822402, sixteenByNine, precision: 6);
        Assert.Equal(52.859924, sixteenByTen, precision: 6);
        Assert.Equal(
            calculator.ConvertHorizontalAngle(45, 4d / 3, 16d / 10),
            sixteenByTen,
            precision: 10);
    }
}
