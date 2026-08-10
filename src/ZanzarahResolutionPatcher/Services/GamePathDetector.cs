namespace ZanzarahResolutionPatcher.Services;

public static class GamePathDetector
{
    public static bool IsSteamInstallation(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return path
            .Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => segment.Equals("steamapps", StringComparison.OrdinalIgnoreCase));
    }
}
