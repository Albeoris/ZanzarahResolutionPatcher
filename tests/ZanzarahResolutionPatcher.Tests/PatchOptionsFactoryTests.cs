using ZanzarahResolutionPatcher.Cli;
using ZanzarahResolutionPatcher.Domain;
using ZanzarahResolutionPatcher.Infrastructure;

namespace ZanzarahResolutionPatcher.Tests;

public sealed class PatchOptionsFactoryTests
{
    private readonly PatchOptionsFactory factory = new();

    [Fact]
    public void Create_WithCombinedResolutions_MapsOptions()
    {
        var settings = new PatchCommandSettings
        {
            OldResolution = "800x600",
            NewResolution = "1920x1080",
            IsUnchecked = true,
            NoBackup = true,
            NonInteractive = true,
            FovFix = true,
        };

        var result = factory.Create(settings);

        Assert.Equal(new Resolution(800, 600), result.OldResolution);
        Assert.Equal(new Resolution(1920, 1080), result.NewResolution);
        Assert.True(result.IsUnchecked);
        Assert.True(result.NoBackup);
        Assert.True(result.NonInteractive);
        Assert.True(result.ApplyFieldOfViewFix);
    }

    [Fact]
    public void Create_WithSeparateDimensions_MapsOptions()
    {
        var settings = new PatchCommandSettings
        {
            OldWidth = "640",
            OldHeight = "480",
            NewWidth = "2560",
            NewHeight = "1440",
        };

        var result = factory.Create(settings);

        Assert.Equal(new Resolution(640, 480), result.OldResolution);
        Assert.Equal(new Resolution(2560, 1440), result.NewResolution);
    }

    [Fact]
    public void Create_WithCombinedAndSeparateNewResolution_ThrowsClearError()
    {
        var settings = new PatchCommandSettings
        {
            NewResolution = "1920x1080",
            NewWidth = "1920",
            NewHeight = "1080",
        };

        var exception = Assert.Throws<UserInputException>(() => factory.Create(settings));

        Assert.Contains("not both", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_WithOnlyOneDimension_ThrowsClearError()
    {
        var settings = new PatchCommandSettings { NewWidth = "1920" };

        var exception = Assert.Throws<UserInputException>(() => factory.Create(settings));

        Assert.Contains("--new-height is required", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("65536")]
    [InlineData("invalid")]
    public void Create_WithInvalidDimension_ThrowsClearError(string width)
    {
        var settings = new PatchCommandSettings { NewWidth = width, NewHeight = "1080" };

        var exception = Assert.Throws<UserInputException>(() => factory.Create(settings));

        Assert.Contains("1 to 65535", exception.Message, StringComparison.Ordinal);
    }
}
