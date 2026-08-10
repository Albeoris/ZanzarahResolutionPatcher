using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ZanzarahResolutionPatcher.Services;

internal sealed class ConsoleWindowDialogHost
{
    private const int RestoreWindowCommand = 9;

    public DialogResult Show(CommonDialog dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        var consoleWindowHandle = GetConsoleWindow();
        if (consoleWindowHandle == IntPtr.Zero)
        {
            return dialog.ShowDialog();
        }

        try
        {
            return dialog.ShowDialog(new WindowOwner(consoleWindowHandle));
        }
        finally
        {
            RestoreFocus(consoleWindowHandle);
        }
    }

    private static void RestoreFocus(IntPtr consoleWindowHandle)
    {
        if (IsIconic(consoleWindowHandle))
        {
            _ = ShowWindow(consoleWindowHandle, RestoreWindowCommand);
        }

        _ = SetForegroundWindow(consoleWindowHandle);
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr windowHandle, int command);

    private sealed class WindowOwner(IntPtr handle) : IWin32Window
    {
        public IntPtr Handle { get; } = handle;
    }
}
