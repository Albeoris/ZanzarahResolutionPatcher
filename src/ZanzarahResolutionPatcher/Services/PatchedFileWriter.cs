namespace ZanzarahResolutionPatcher.Services;

public sealed class PatchedFileWriter
{
    public void Write(string outputPath, byte[] bytes, string? backupPath, bool createBackup, string inputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);

        var outputDirectory = Path.GetDirectoryName(outputPath)
            ?? throw new IOException("The output path does not have a parent directory.");
        var temporaryPath = Path.Combine(
            outputDirectory,
            $".{Path.GetFileName(outputPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 64 * 1024,
                FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            if (createBackup)
            {
                if (backupPath is null)
                {
                    throw new InvalidOperationException("The backup path has not been resolved.");
                }

                File.Copy(inputPath, backupPath, overwrite: false);
            }

            File.Move(temporaryPath, outputPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
