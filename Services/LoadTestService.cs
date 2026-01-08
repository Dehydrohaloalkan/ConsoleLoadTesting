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
        CancellationToken cancellationToken = default)
    {
        var results = new List<TestResult>();
        var totalRequests = config.RequestCount * config.VirtualUsers;
        var completedRequests = 0;
        var random = new Random();

        var tasks = new List<Task>();

        for (int userId = 0; userId < config.VirtualUsers; userId++)
        {
            var userTasks = Task.Run(async () =>
            {
                var userResults = new List<TestResult>();

                for (int i = 0; i < config.RequestCount; i++)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    string url;
                    if (config.UrlMode == UrlMode.Random)
                    {
                        url = config.Urls[random.Next(config.Urls.Count)];
                    }
                    else
                    {
                        url = config.Urls[i % config.Urls.Count];
                    }

                    var result = await ExecuteRequestAsync(userId, url, config.Headers);
                    userResults.Add(result);

                    var completed = Interlocked.Increment(ref completedRequests);
                    progress?.Report((double)completed / totalRequests);

                    if (config.DelayMs > 0 && i < config.RequestCount - 1)
                    {
                        await Task.Delay(config.DelayMs, cancellationToken);
                    }
                }

                lock (results)
                {
                    results.AddRange(userResults);
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
        var result = new TestResult
        {
            UserId = userId,
            Url = url
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
}
