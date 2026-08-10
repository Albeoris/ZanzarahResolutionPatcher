using ZanzarahResolutionPatcher.Domain;

namespace ZanzarahResolutionPatcher.Tests;

public sealed class PatchOptionsTests
{
    [Fact]
    public void SetReplacement_ForExistingOldResolution_UpdatesMappingWithoutAddingDuplicate()
    {
        var oldResolution = new Resolution(800, 600);
        var options = new PatchOptions();
        options.SetReplacement(oldResolution, new Resolution(1280, 720));

        options.SetReplacement(oldResolution, new Resolution(1920, 1080));

        var replacement = Assert.Single(options.Replacements);
        Assert.Equal(oldResolution, replacement.OldResolution);
        Assert.Equal(new Resolution(1920, 1080), replacement.NewResolution);
    }

    [Fact]
    public void SetReplacement_WithIdenticalResolutions_RejectsMapping()
    {
        var resolution = new Resolution(800, 600);
        var options = new PatchOptions();

        var exception = Assert.Throws<ArgumentException>(
            () => options.SetReplacement(resolution, resolution));

        Assert.Contains("must be different", exception.Message, StringComparison.Ordinal);
    }
}
