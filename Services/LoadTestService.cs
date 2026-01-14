using System.Diagnostics;
using System.Net.Http.Headers;
using Spectre.Console;
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

                    var url = config.UrlMode == UrlMode.Random
                        ? config.Urls[random.Next(config.Urls.Count)]
                        : config.Urls[i % config.Urls.Count];

                    var result = await ExecuteRequestAsync(userId, url, config.Headers);
                    userResults.Add(result);
                    onResultReceived?.Invoke(result);

                    progress?.Report((double)Interlocked.Increment(ref completedRequests) / totalRequests);

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

    public async Task<List<TestResult>> RunScenariosLoadTestAsync(
        TestConfig config,
        IProgress<double>? progress = null,
        Action<TestResult>? onResultReceived = null,
        CancellationToken cancellationToken = default)
    {
        if (!config.UseScenarios || config.Scenarios.Count == 0)
        {
            throw new ArgumentException("Конфигурация не содержит сценариев");
        }

        // Калибровка для определения среднего времени ответа
        var avgResponseTime = await CalibrateResponseTimeAsync(config, cancellationToken);

        var allResults = new List<TestResult>();
        var random = new Random();
        var scenarioIndex = 0;

        foreach (var scenario in config.Scenarios)
        {
            scenarioIndex++;
            AnsiConsole.MarkupLine($"[bold cyan]Запуск сценария {scenarioIndex}/{config.Scenarios.Count}: {scenario.VirtualUsers} пользователей, {scenario.RequestCount} запросов, {scenario.DurationSeconds} секунд[/]");

            var scenarioResults = await RunScenarioAsync(scenario, config, avgResponseTime, random, progress, onResultReceived, cancellationToken);
            allResults.AddRange(scenarioResults);

            AnsiConsole.MarkupLine($"[green]Сценарий {scenarioIndex} завершен[/]");
            AnsiConsole.WriteLine();
        }

        return allResults;
    }

    private async Task<List<TestResult>> RunScenarioAsync(
        ScenarioConfig scenario,
        TestConfig config,
        long avgResponseTime,
        Random random,
        IProgress<double>? progress,
        Action<TestResult>? onResultReceived,
        CancellationToken cancellationToken)
    {
        var results = new List<TestResult>();
        var scenarioStartTime = DateTime.UtcNow;
        var scenarioEndTime = scenarioStartTime.AddSeconds(scenario.DurationSeconds);

        // Рассчитываем общее количество запросов, которое нужно выполнить
        var totalRequests = scenario.VirtualUsers * scenario.RequestCount;
        var completedRequests = 0;

        // Рассчитываем задержку между запросами для равномерного распределения
        // Учитываем среднее время ответа сервера
        var totalTimePerUser = TimeSpan.FromSeconds(scenario.DurationSeconds);
        var delayBetweenRequests = Math.Max(0, (totalTimePerUser.TotalMilliseconds - avgResponseTime) / scenario.RequestCount);

        var tasks = new List<Task>();

        for (int userId = 0; userId < scenario.VirtualUsers; userId++)
        {
            var userTasks = Task.Run(async () =>
            {
                var userResults = new List<TestResult>();
                var userStartTime = DateTime.UtcNow;

                for (int requestIndex = 0; requestIndex < scenario.RequestCount; requestIndex++)
                {
                    if (cancellationToken.IsCancellationRequested || DateTime.UtcNow >= scenarioEndTime)
                        break;

                    var url = config.UrlMode == UrlMode.Random
                        ? config.Urls[random.Next(config.Urls.Count)]
                        : config.Urls[requestIndex % config.Urls.Count];

                    var result = await ExecuteRequestAsync(userId, url, config.Headers);
                    userResults.Add(result);
                    onResultReceived?.Invoke(result);

                    progress?.Report((double)Interlocked.Increment(ref completedRequests) / totalRequests);

                    // Задержка между запросами
                    if (delayBetweenRequests > 0 && requestIndex < scenario.RequestCount - 1)
                    {
                        await Task.Delay((int)delayBetweenRequests, cancellationToken);
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

        // Фильтруем результаты, оставляя только те, которые были выполнены в рамках сценария
        results = results.Where(r => r.Timestamp >= scenarioStartTime && r.Timestamp <= scenarioEndTime).ToList();

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

    public async Task<long> CalibrateResponseTimeAsync(TestConfig config, CancellationToken cancellationToken = default)
    {
        var calibrationResults = new List<long>();
        var random = new Random();

        AnsiConsole.MarkupLine($"[yellow]Выполняем калибровку ({config.CalibrationRequests} тестовых запросов)...[/]");

        for (int i = 0; i < config.CalibrationRequests; i++)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var url = config.Urls[random.Next(config.Urls.Count)];
            var result = await ExecuteRequestAsync(-1, url, config.Headers);

            if (result.IsSuccess)
            {
                calibrationResults.Add(result.ResponseTimeMs);
            }
            else
            {
                AnsiConsole.MarkupLine($"[yellow]Тестовый запрос #{i + 1} неудачен, пропускаем...[/]");
            }

            // Небольшая задержка между калибровочными запросами
            if (i < config.CalibrationRequests - 1)
            {
                await Task.Delay(100, cancellationToken);
            }
        }

        if (calibrationResults.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Все тестовые запросы неудачны, используем значение по умолчанию 500мс[/]");
            return 500;
        }

        var avgResponseTime = (long)calibrationResults.Average();
        AnsiConsole.MarkupLine($"[green]Среднее время ответа: {avgResponseTime}мс[/]");

        return avgResponseTime;
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
