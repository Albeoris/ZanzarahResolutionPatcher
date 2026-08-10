using ZanzarahResolutionPatcher.Services;

namespace ZanzarahResolutionPatcher.Tests;

public sealed class GamePathDetectorTests
{
    [Theory]
    [InlineData(@"C:\Steam\steamapps\common\ZanZarah\System\zanthp.exe")]
    [InlineData(@"D:\STEAMAPPS\common\ZanZarah\System\zanthp.exe")]
    [InlineData("C:/Steam/steamapps/common/ZanZarah/System/zanthp.exe")]
    public void IsSteamInstallation_WithSteamAppsSegment_ReturnsTrue(string path)
    {
        Assert.True(GamePathDetector.IsSteamInstallation(path));
    }

    [Theory]
    [InlineData(@"C:\Games\ZanZarah\System\zanthp.exe")]
    [InlineData(@"C:\Games\my-steamapps-backup\ZanZarah\zanthp.exe")]
    [InlineData(@"C:\Games\steamapps.exe\ZanZarah\zanthp.exe")]
    public void IsSteamInstallation_WithoutExactSteamAppsSegment_ReturnsFalse(string path)
    {
        Assert.False(GamePathDetector.IsSteamInstallation(path));
    }
}
