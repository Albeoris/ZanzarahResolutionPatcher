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

    [Fact]
    public void ResolveFinalResolutions_WhenReplacementCreatesDuplicate_RejectsPlan()
    {
        var resolution640 = new Resolution(640, 480);
        var resolution800 = new Resolution(800, 600);
        var resolution1024 = new Resolution(1024, 768);
        var options = new PatchOptions();
        options.SetReplacement(resolution800, resolution640);

        var exception = Assert.Throws<InvalidOperationException>(
            () => options.ResolveFinalResolutions([resolution640, resolution800, resolution1024]));

        Assert.Contains("duplicate", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("640x480", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveFinalResolutions_WhenResolutionsAreSwapped_AllowsUniquePlan()
    {
        var resolution640 = new Resolution(640, 480);
        var resolution800 = new Resolution(800, 600);
        var resolution1024 = new Resolution(1024, 768);
        var options = new PatchOptions();
        options.SetReplacement(resolution640, resolution800);
        options.SetReplacement(resolution800, resolution640);

        var result = options.ResolveFinalResolutions(
            [resolution640, resolution800, resolution1024]);

        Assert.Equal([resolution800, resolution640, resolution1024], result);
    }

    [Fact]
    public void GetUnavailableTargetResolutions_ExcludesCurrentSlotAndIncludesOtherFinalTargets()
    {
        var resolution640 = new Resolution(640, 480);
        var resolution800 = new Resolution(800, 600);
        var resolution1024 = new Resolution(1024, 768);
        var resolution720p = new Resolution(1280, 720);
        var options = new PatchOptions();
        options.SetReplacement(resolution800, resolution720p);

        var result = options.GetUnavailableTargetResolutions(
            [resolution640, resolution800, resolution1024],
            resolution1024);

        Assert.Equal([resolution640, resolution720p], result);
        Assert.DoesNotContain(resolution800, result);
        Assert.DoesNotContain(resolution1024, result);
    }

    [Fact]
    public void GetAvailableTargetResolutions_ExcludesCurrentAndOccupiedResolutions()
    {
        var resolution640 = new Resolution(640, 480);
        var resolution800 = new Resolution(800, 600);
        var resolution1024 = new Resolution(1024, 768);
        var resolution720p = new Resolution(1280, 720);
        var options = new PatchOptions();

        var result = options.GetAvailableTargetResolutions(
            [resolution640, resolution800, resolution1024],
            resolution640,
            [resolution640, resolution800, resolution720p, resolution720p]);

        Assert.Equal([resolution720p], result);
    }
}
