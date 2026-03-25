using System.Text;

namespace ConsoleLoadTesting.Services;

public static class FileService
{
    public static void EnsureOutputDirectory(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public static StreamReader CreateSequentialReader(string filePath)
    {
        var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            bufferSize: 65536,
            options: FileOptions.SequentialScan);

        return new StreamReader(stream);
    }

    public static StreamWriter CreateNewUtf8Writer(string filePath)
    {
        EnsureOutputDirectory(filePath);

        var stream = new FileStream(
            filePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 65536,
            useAsync: true);

        return new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), 65536);
    }

    public static string ResolveOutputPath(string? savePath, string filePrefix, string extension)
    {
        if (!string.IsNullOrWhiteSpace(savePath))
        {
            return Path.GetFullPath(savePath);
        }

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        return Path.Combine(Path.GetTempPath(), $"{filePrefix}_{timestamp}{extension}");
    }
}

