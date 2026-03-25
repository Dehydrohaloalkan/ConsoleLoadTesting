using System.Threading.Channels;
using ConsoleLoadTesting.Models;

namespace ConsoleLoadTesting.Services;

public sealed class ResultWriter : IAsyncDisposable
{
    private readonly Channel<TestResult[]> _channel;
    private readonly StreamWriter _writer;
    private readonly Task _writerTask;
    private bool _completed;

    public ResultWriter(string filePath)
    {
        FilePath = filePath;
        _writer = FileService.CreateNewUtf8Writer(filePath);
        _channel = Channel.CreateUnbounded<TestResult[]>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        _writerTask = Task.Run(ProcessWritesAsync);
    }

    public string FilePath { get; }

    public ValueTask EnqueueAsync(TestResult[] batch, CancellationToken cancellationToken = default)
    {
        if (batch.Length == 0)
        {
            return ValueTask.CompletedTask;
        }

        return _channel.Writer.WriteAsync(batch, cancellationToken);
    }

    public async Task CompleteAsync()
    {
        if (_completed)
        {
            await _writerTask.ConfigureAwait(false);
            return;
        }

        _completed = true;
        _channel.Writer.TryComplete();
        await _writerTask.ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await CompleteAsync().ConfigureAwait(false);
        await _writer.DisposeAsync().ConfigureAwait(false);
    }

    private async Task ProcessWritesAsync()
    {
        await _writer.WriteLineAsync(TestResultCsv.Header).ConfigureAwait(false);

        await foreach (var batch in _channel.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            foreach (var result in batch)
            {
                await _writer.WriteLineAsync(TestResultCsv.Serialize(result)).ConfigureAwait(false);
            }

            await _writer.FlushAsync().ConfigureAwait(false);
        }
    }
}
