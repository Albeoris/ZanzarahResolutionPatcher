using System.Windows.Forms;

namespace ZanzarahResolutionPatcher.Services;

public interface IFileDialogService
{
    string? SelectInputFile();

    string? SelectOutputFile(string inputPath);
}

public sealed class FileDialogService : IFileDialogService
{
    private readonly ConsoleWindowDialogHost dialogHost = new();

    public string? SelectInputFile()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Select the Zanzarah game executable",
            Filter = "Zanzarah executable (zanthp.exe)|zanthp.exe|Executable files (*.exe)|*.exe",
            CheckFileExists = true,
            CheckPathExists = true,
            Multiselect = false,
            RestoreDirectory = true,
        };

        return dialogHost.Show(dialog) == DialogResult.OK ? dialog.FileName : null;
    }

    public string? SelectOutputFile(string inputPath)
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Save the patched Zanzarah executable",
            Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
            FileName = Path.GetFileName(inputPath),
            InitialDirectory = Path.GetDirectoryName(inputPath),
            AddExtension = true,
            DefaultExt = "exe",
            OverwritePrompt = true,
            RestoreDirectory = true,
        };

        return dialogHost.Show(dialog) == DialogResult.OK ? dialog.FileName : null;
    }
}
