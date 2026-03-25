namespace ConsoleLoadTesting.Services;

public static class ErrorLogService
{
    public static async Task<string> WriteAsync(Exception exception, CancellationToken cancellationToken = default)
    {
        var path = FileService.ResolveOutputPath(null, "ConsoleLoadTesting_error", ".log");

        await using var writer = FileService.CreateNewUtf8Writer(path);
        await writer.WriteLineAsync(DateTime.UtcNow.ToString("O")).ConfigureAwait(false);
        await writer.WriteLineAsync(exception.ToString()).ConfigureAwait(false);
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);

        return path;
    }
}

