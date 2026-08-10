using System.Runtime.InteropServices;
using ZanzarahResolutionPatcher.Domain;

namespace ZanzarahResolutionPatcher.Services;

public interface ISupportedResolutionProvider
{
    IReadOnlyList<Resolution> GetSupportedResolutions();
}

public sealed class SupportedResolutionProvider : ISupportedResolutionProvider
{
    private const int EnumCurrentSettings = -1;

    public IReadOnlyList<Resolution> GetSupportedResolutions()
    {
        var resolutions = new SortedSet<Resolution>();
        var mode = CreateMode();

        for (var modeIndex = 0; EnumDisplaySettings(null, modeIndex, ref mode); modeIndex++)
        {
            if (mode.PelsWidth is > 0 and <= ushort.MaxValue &&
                mode.PelsHeight is > 0 and <= ushort.MaxValue)
            {
                resolutions.Add(new Resolution((ushort)mode.PelsWidth, (ushort)mode.PelsHeight));
            }

            mode = CreateMode();
        }

        if (resolutions.Count == 0)
        {
            mode = CreateMode();
            if (EnumDisplaySettings(null, EnumCurrentSettings, ref mode) &&
                mode.PelsWidth is > 0 and <= ushort.MaxValue &&
                mode.PelsHeight is > 0 and <= ushort.MaxValue)
            {
                resolutions.Add(new Resolution((ushort)mode.PelsWidth, (ushort)mode.PelsHeight));
            }
        }

        return [.. resolutions];
    }

    private static DevMode CreateMode() => new()
    {
        DeviceName = string.Empty,
        FormName = string.Empty,
        Size = checked((ushort)Marshal.SizeOf<DevMode>()),
    };

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplaySettings(
        string? deviceName,
        int modeNumber,
        ref DevMode deviceMode);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DevMode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
        public ushort SpecVersion;
        public ushort DriverVersion;
        public ushort Size;
        public ushort DriverExtra;
        public uint Fields;
        public int PositionX;
        public int PositionY;
        public uint DisplayOrientation;
        public uint DisplayFixedOutput;
        public short Color;
        public short Duplex;
        public short YResolution;
        public short TTOption;
        public short Collate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string FormName;
        public ushort LogPixels;
        public uint BitsPerPel;
        public uint PelsWidth;
        public uint PelsHeight;
        public uint DisplayFlags;
        public uint DisplayFrequency;
        public uint IcmMethod;
        public uint IcmIntent;
        public uint MediaType;
        public uint DitherType;
        public uint Reserved1;
        public uint Reserved2;
        public uint PanningWidth;
        public uint PanningHeight;
    }
}
