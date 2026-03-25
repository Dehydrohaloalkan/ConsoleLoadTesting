using System.Diagnostics;
using System.Net.Http.Headers;
using ConsoleLoadTesting.Models;

namespace ConsoleLoadTesting.Services;

public class LoadTestService
{
    private readonly HttpClient _httpClient;

    public LoadTestService()
    {
        _httpClient = new HttpClient();
    }

    public async Task<List<TestResult>> RunLoadTestAsync(
        TestConfig config,
        IProgress<double>? progress = null,
        Action<TestResult>? onResultReceived = null,
        ResultWriter? resultWriter = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<TestResult>();
        var totalRequests = config.RequestCount * config.VirtualUsers;
        var completedRequests = 0;
        var tasks = new List<Task>();

        for (int userId = 0; userId < config.VirtualUsers; userId++)
        {
            var localUserId = userId;
            var userTasks = Task.Run(async () =>
            {
                var bufferCapacity = GetBufferCapacity(config.RequestCount);
                var userBuffer = new List<TestResult>(bufferCapacity);

                try
                {
                    for (int i = 0; i < config.RequestCount; i++)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            break;

                        var url = config.UrlMode == UrlMode.Random
                            ? config.Urls[Random.Shared.Next(config.Urls.Count)]
                            : config.Urls[i % config.Urls.Count];

                        var result = await ExecuteRequestAsync(localUserId, url, config.Headers);
                        userBuffer.Add(result);
                        onResultReceived?.Invoke(result);

                        progress?.Report((double)Interlocked.Increment(ref completedRequests) / totalRequests);

                        if (userBuffer.Count >= bufferCapacity)
                        {
                            await FlushBufferAsync(userBuffer, results, resultWriter).ConfigureAwait(false);
                        }

                        if (config.DelayMs > 0 && i < config.RequestCount - 1)
                        {
                            await Task.Delay(config.DelayMs, cancellationToken);
                        }
                    }
                }
                finally
                {
                    await FlushBufferAsync(userBuffer, results, resultWriter).ConfigureAwait(false);
                }
            }, cancellationToken);

            tasks.Add(userTasks);
        }

        await Task.WhenAll(tasks);
        return results;
    }

    private async Task<TestResult> ExecuteRequestAsync(int userId, string url, Dictionary<string, string> headers)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestStartTime = DateTime.UtcNow;
        var result = new TestResult
        {
            UserId = userId,
            Url = url,
            Timestamp = requestStartTime
        };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            
            foreach (var header in headers)
            {
                if (header.Key.Equals("User-Agent", StringComparison.OrdinalIgnoreCase))
                {
                    request.Headers.UserAgent.ParseAdd(header.Value);
                }
                else
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            var response = await _httpClient.SendAsync(request);
            stopwatch.Stop();

            result.StatusCode = (int)response.StatusCode;
            result.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
            result.IsSuccess = response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.StatusCode = 0;
            result.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
            result.IsSuccess = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }

    private static int GetBufferCapacity(int requestCount)
    {
        return Math.Max(1, Math.Min(100, requestCount));
    }

    private static async Task FlushBufferAsync(
        List<TestResult> userBuffer,
        List<TestResult> fallbackResults,
        ResultWriter? resultWriter)
    {
        if (userBuffer.Count == 0)
        {
            return;
        }

        if (resultWriter is not null)
        {
            await resultWriter.EnqueueAsync(userBuffer.ToArray()).ConfigureAwait(false);
        }
        else
        {
            lock (fallbackResults)
            {
                fallbackResults.AddRange(userBuffer);
            }
        }

        userBuffer.Clear();
    }
}
