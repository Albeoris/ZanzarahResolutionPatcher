using ZanzarahResolutionPatcher.Domain;

namespace ZanzarahResolutionPatcher.Tests;

public sealed class ResolutionTests
{
    [Theory]
    [InlineData("1920x1080", 1920, 1080)]
    [InlineData(" 800 x 600 ", 800, 600)]
    [InlineData("65535x65535", 65535, 65535)]
    public void TryParse_WithValidText_ReturnsResolution(string text, ushort width, ushort height)
    {
        var parsed = Resolution.TryParse(text, out var resolution);

        Assert.True(parsed);
        Assert.Equal(new Resolution(width, height), resolution);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1920")]
    [InlineData("1920*1080")]
    [InlineData("0x1080")]
    [InlineData("65536x1080")]
    [InlineData("-1x1080")]
    public void TryParse_WithInvalidText_ReturnsFalse(string? text)
    {
        Assert.False(Resolution.TryParse(text, out _));
    }
}
