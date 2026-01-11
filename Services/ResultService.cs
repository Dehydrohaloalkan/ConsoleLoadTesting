using System.Globalization;
using Spectre.Console;
using ConsoleLoadTesting.Models;

namespace ConsoleLoadTesting.Services;

public class ResultService
{
    public void DisplayResults(List<TestResult> results, int chartTimeStepSeconds = 1)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Результаты нагрузочного тестирования[/]");
        AnsiConsole.WriteLine();

        DisplaySuccessFailureChart(results);
        DisplayTimeStatistics(results);
        DisplayStatusCodesTable(results);
        DisplayUrlStatisticsTable(results);
        DisplayErrorUrlsTable(results);
        
        if (results.Any())
        {
            AnsiConsole.WriteLine();
            DisplayTimeCharts(results, chartTimeStepSeconds);
        }
    }

    private void DisplaySuccessFailureChart(List<TestResult> results)
    {
        var successCount = results.Count(r => r.IsSuccess);
        var failureCount = results.Count - successCount;

        var chart = new BarChart()
            .Width(60)
            .Label("[green]Успешные/Неуспешные запросы[/]")
            .AddItem("Успешные", successCount, Color.Green)
            .AddItem("Неуспешные", failureCount, Color.Red);

        AnsiConsole.Write(chart);
        AnsiConsole.WriteLine();
    }

    private void DisplayTimeStatistics(List<TestResult> results)
    {
        if (!results.Any())
            return;

        var avgTime = results.Average(r => r.ResponseTimeMs);
        var percentile95 = CalculatePercentile(results.Select(r => r.ResponseTimeMs).ToList(), 0.95);

        AnsiConsole.MarkupLine($"[bold]Среднее время запроса:[/] {avgTime:F2} мс");
        AnsiConsole.MarkupLine($"[bold]95 процентиль по времени:[/] {percentile95} мс");
        AnsiConsole.WriteLine();
    }

    private void DisplayStatusCodesTable(List<TestResult> results)
    {
        var statusGroups = results
            .Where(r => r.StatusCode > 0)
            .GroupBy(r => r.StatusCode)
            .OrderBy(g => g.Key)
            .ToList();

        if (!statusGroups.Any()) return;

        AnsiConsole.MarkupLine("[bold]Статусы ответов:[/]");
        var statusTable = new Table();
        statusTable.AddColumn("Статус");
        statusTable.AddColumn("Количество");

        foreach (var group in statusGroups)
        {
            var color = GetStatusCodeColor(group.Key);
            statusTable.AddRow($"[{color}]{group.Key}[/]", group.Count().ToString());
        }

        AnsiConsole.Write(statusTable);
        AnsiConsole.WriteLine();
    }

    private void DisplayUrlStatisticsTable(List<TestResult> results)
    {
        var uniqueUrls = results.Select(r => r.Url).Distinct().ToList();
        if (uniqueUrls.Count <= 1) return;

        AnsiConsole.MarkupLine("[bold]Статистика по ссылкам:[/]");
        AnsiConsole.WriteLine();

        var urlStatsTable = new Table();
        urlStatsTable.AddColumn("URL");
        urlStatsTable.AddColumn("Всего запросов");
        urlStatsTable.AddColumn("Успешных");
        urlStatsTable.AddColumn("Неуспешных");
        urlStatsTable.AddColumn("Среднее время (мс)");
        urlStatsTable.AddColumn("95 процентиль (мс)");
        urlStatsTable.AddColumn("Мин/Макс (мс)");

        foreach (var url in uniqueUrls)
        {
            var urlResults = results.Where(r => r.Url == url).ToList();
            var stats = CalculateUrlStatistics(urlResults);
            
            urlStatsTable.AddRow(
                TruncateUrl(url, 50),
                urlResults.Count.ToString(),
                $"[green]{stats.SuccessCount}[/]",
                stats.FailureCount > 0 ? $"[red]{stats.FailureCount}[/]" : "0",
                $"{stats.AvgTime:F2}",
                stats.Percentile95.ToString(),
                $"{stats.MinTime}/{stats.MaxTime}"
            );
        }

        AnsiConsole.Write(urlStatsTable);
        AnsiConsole.WriteLine();
    }

    private void DisplayErrorUrlsTable(List<TestResult> results)
    {
        var errorUrls = results
            .Where(r => !r.IsSuccess)
            .GroupBy(r => r.Url)
            .ToList();

        if (!errorUrls.Any()) return;

        AnsiConsole.MarkupLine("[bold red]Ссылки с ошибками:[/]");
        var errorTable = new Table();
        errorTable.AddColumn("URL");
        errorTable.AddColumn("Количество ошибок");
        errorTable.AddColumn("Последняя ошибка");

        foreach (var group in errorUrls)
        {
            var lastError = group.Last();
            errorTable.AddRow(
                group.Key,
                group.Count().ToString(),
                lastError.ErrorMessage ?? $"HTTP {lastError.StatusCode}"
            );
        }

        AnsiConsole.Write(errorTable);
    }

    public void SaveResultsToFile(List<TestResult> results, string filePath)
    {
        // Создаём директорию, если её нет
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var lines = new List<string>
        {
            "UserId,Url,StatusCode,TimeMs,Timestamp,IsSuccess,ErrorMessage"
        };

        foreach (var result in results)
        {
            var timestampStr = result.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture);
            lines.Add($"{result.UserId},{EscapeCsv(result.Url)},{result.StatusCode},{result.ResponseTimeMs},{timestampStr},{result.IsSuccess},{EscapeCsv(result.ErrorMessage ?? string.Empty)}");
        }

        File.WriteAllLines(filePath, lines);
        AnsiConsole.MarkupLine($"[green]Результаты сохранены в: {filePath}[/]");
    }

    private string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }

    public List<TestResult>? LoadResultsFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            AnsiConsole.MarkupLine($"[red]Файл не найден: {filePath}[/]");
            return null;
        }

        try
        {
            var lines = File.ReadAllLines(filePath).ToList();
            if (lines.Count < 1)
            {
                AnsiConsole.MarkupLine($"[red]Пустой файл: {filePath}[/]");
                return null;
            }

            // Первая строка - заголовки (пропускаем)
            var results = new List<TestResult>();
            for (int i = 1; i < lines.Count; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var parts = ParseCsvLine(line);
                if (parts.Count >= 6)
                {
                    var result = new TestResult
                    {
                        UserId = int.Parse(parts[0]),
                        Url = parts[1],
                        StatusCode = int.Parse(parts[2]),
                        ResponseTimeMs = long.Parse(parts[3]),
                        Timestamp = DateTime.Parse(parts[4], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                        IsSuccess = bool.Parse(parts[5]),
                        ErrorMessage = parts.Count > 6 && !string.IsNullOrEmpty(parts[6]) ? parts[6] : null
                    };
                    results.Add(result);
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Ошибка при загрузке файла {filePath}: {ex.Message}[/]");
            return null;
        }
    }

    private List<string> ParseCsvLine(string line)
    {
        var parts = new List<string>();
        var currentPart = new System.Text.StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    // Экранированная кавычка
                    currentPart.Append('"');
                    i++; // Пропускаем следующую кавычку
                }
                else
                {
                    // Начало или конец кавычек
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                // Разделитель вне кавычек
                parts.Add(currentPart.ToString());
                currentPart.Clear();
            }
            else
            {
                currentPart.Append(c);
            }
        }

        // Добавляем последнюю часть
        parts.Add(currentPart.ToString());
        return parts;
    }

    public void DisplaySummaryReport(Dictionary<string, List<TestResult>> testResultsDict, int chartTimeStepSeconds = 1)
    {
        AnsiConsole.MarkupLine("[bold cyan]Сводный отчет по результатам тестирования[/]");
        AnsiConsole.WriteLine();

        if (testResultsDict.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Нет данных для отображения[/]");
            return;
        }

        DisplayTestsSummaryTable(testResultsDict);
        
        var allResults = testResultsDict.Values.SelectMany(r => r).ToList();
        if (allResults.Any())
        {
            DisplayOverallStatisticsTable(allResults);
            DisplaySummaryUrlStatisticsTable(allResults);
            DisplayTimeCharts(allResults, chartTimeStepSeconds);
        }
    }

    private void DisplayTestsSummaryTable(Dictionary<string, List<TestResult>> testResultsDict)
    {
        var summaryTable = new Table();
        summaryTable.AddColumn("Файл");
        summaryTable.AddColumn("Время теста");
        summaryTable.AddColumn("Запросов");
        summaryTable.AddColumn("Успешных");
        summaryTable.AddColumn("Неуспешных");
        summaryTable.AddColumn("Среднее (мс)");
        summaryTable.AddColumn("95 процентиль");
        summaryTable.AddColumn("RPS");
        summaryTable.Border = TableBorder.Rounded;
        summaryTable.Title = new TableTitle("[bold]Сводная информация по тестам[/]");

        foreach (var kvp in testResultsDict)
        {
            var results = kvp.Value;
            if (!results.Any()) continue;

            var stats = CalculateBasicStatistics(results);
            var duration = CalculateDuration(results);
            var rps = CalculateRps(results.Count, duration);

            summaryTable.AddRow(
                Path.GetFileName(kvp.Key),
                duration.ToString(@"mm\:ss"),
                results.Count.ToString(),
                $"[green]{stats.SuccessCount}[/]",
                stats.FailureCount > 0 ? $"[red]{stats.FailureCount}[/]" : "0",
                $"{stats.AvgTime:F1}",
                stats.Percentile95.ToString(),
                $"{rps:F2}"
            );
        }

        AnsiConsole.Write(summaryTable);
        AnsiConsole.WriteLine();
    }

    private void DisplayOverallStatisticsTable(List<TestResult> allResults)
    {
        var stats = CalculateBasicStatistics(allResults);
        var duration = CalculateDuration(allResults);
        var rps = CalculateRps(allResults.Count, duration);

        AnsiConsole.MarkupLine("[bold]Общая статистика по всем тестам:[/]");
        AnsiConsole.WriteLine();
        
        var overallTable = new Table();
        overallTable.AddColumn("Метрика");
        overallTable.AddColumn("Значение");
        overallTable.Border = TableBorder.Rounded;
        
        overallTable.AddRow("Всего запросов", allResults.Count.ToString());
        overallTable.AddRow("Успешных", $"[green]{stats.SuccessCount}[/]");
        overallTable.AddRow("Неуспешных", stats.FailureCount > 0 ? $"[red]{stats.FailureCount}[/]" : "0");
        overallTable.AddRow("Среднее время (мс)", $"{stats.AvgTime:F2}");
        overallTable.AddRow("95 процентиль (мс)", stats.Percentile95.ToString());
        overallTable.AddRow("Общий RPS", $"{rps:F2}");
        
        AnsiConsole.Write(overallTable);
        AnsiConsole.WriteLine();
    }

    private void DisplaySummaryUrlStatisticsTable(List<TestResult> allResults)
    {
        var allUrls = allResults.Select(r => r.Url).Distinct().ToList();
        if (allUrls.Count <= 1) return;

        AnsiConsole.MarkupLine("[bold]Статистика по URL:[/]");
        AnsiConsole.WriteLine();
        
        var urlTable = new Table();
        urlTable.AddColumn("URL");
        urlTable.AddColumn("Всего запросов");
        urlTable.AddColumn("Успешных");
        urlTable.AddColumn("Неуспешных");
        urlTable.AddColumn("Среднее время (мс)");
        urlTable.AddColumn("95 процентиль (мс)");
        urlTable.Border = TableBorder.Rounded;
        
        foreach (var url in allUrls)
        {
            var urlResults = allResults.Where(r => r.Url == url).ToList();
            var stats = CalculateUrlStatistics(urlResults);
            
            urlTable.AddRow(
                TruncateUrl(url, 50),
                urlResults.Count.ToString(),
                $"[green]{stats.SuccessCount}[/]",
                stats.FailureCount > 0 ? $"[red]{stats.FailureCount}[/]" : "0",
                $"{stats.AvgTime:F2}",
                stats.Percentile95.ToString()
            );
        }
        
        AnsiConsole.Write(urlTable);
        AnsiConsole.WriteLine();
    }

    private void DisplayTimeCharts(List<TestResult> results, int stepSeconds)
    {
        if (!results.Any())
            return;

        var startTime = results.Min(r => r.Timestamp);
        var endTime = results.Max(r => r.Timestamp);

        // Группируем запросы по временным интервалам
        var requestsByTime = results
            .GroupBy(r => GetTimeInterval(r.Timestamp, startTime, stepSeconds))
            .OrderBy(g => g.Key)
            .ToList();

        // Группируем ошибки по временным интервалам
        var errorsByTime = results
            .Where(r => !r.IsSuccess)
            .GroupBy(r => GetTimeInterval(r.Timestamp, startTime, stepSeconds))
            .OrderBy(g => g.Key)
            .ToList();

        if (!requestsByTime.Any())
            return;

        AnsiConsole.MarkupLine("[bold]Графики по времени:[/]");
        AnsiConsole.WriteLine();

        // Создаем таблицу для размещения графиков рядом
        var chartTable = new Table();
        chartTable.AddColumn("Время");
        chartTable.AddColumn("RPS");
        chartTable.AddColumn("Ошибки");
        chartTable.Border = TableBorder.Rounded;
        chartTable.ShowHeaders = true;

        // Находим максимальное значение для масштабирования
        var maxRequests = requestsByTime.Any() ? requestsByTime.Max(g => g.Count()) : 1;
        var maxErrors = errorsByTime.Any() ? errorsByTime.Max(g => g.Count()) : 1;

        // Создаем словари для быстрого поиска
        var requestsDict = requestsByTime.ToDictionary(g => g.Key, g => g.Count());
        var errorsDict = errorsByTime.ToDictionary(g => g.Key, g => g.Count());

        // Получаем все временные интервалы
        var allIntervals = requestsByTime.Select(g => g.Key).Union(errorsDict.Keys).OrderBy(t => t).ToList();

        foreach (var interval in allIntervals)
        {
            var requestCount = requestsDict.GetValueOrDefault(interval, 0);
            var errorCount = errorsDict.GetValueOrDefault(interval, 0);
            
            var requestsBar = CreateBar(requestCount, maxRequests, 35, Color.Blue);
            var errorsBar = CreateBar(errorCount, maxErrors, 35, Color.Red);
            
            var timeLabel = startTime.AddSeconds(interval * stepSeconds).ToString("HH:mm:ss");
            
            chartTable.AddRow(
                timeLabel,
                $"{requestsBar} [blue]{requestCount}[/]",
                $"{errorsBar} [red]{errorCount}[/]"
            );
        }

        AnsiConsole.Write(chartTable);
        AnsiConsole.WriteLine();
    }

    private int GetTimeInterval(DateTime timestamp, DateTime startTime, int stepSeconds)
    {
        var seconds = (int)(timestamp - startTime).TotalSeconds;
        return seconds / stepSeconds;
    }

    private string CreateBar(int value, int maxValue, int width, Color color)
    {
        if (maxValue == 0)
            return new string(' ', width);

        var barWidth = (int)((double)value / maxValue * width);
        var bar = new string('█', barWidth);
        return $"[{color}]{bar}[/]";
    }

    // Helper методы для расчета статистики
    private long CalculatePercentile(List<long> sortedValues, double percentile)
    {
        if (!sortedValues.Any()) return 0;
        var sorted = sortedValues.OrderBy(v => v).ToList();
        var index = (int)(sorted.Count * percentile);
        return index < sorted.Count ? sorted[index] : sorted.LastOrDefault();
    }

    private BasicStatistics CalculateBasicStatistics(List<TestResult> results)
    {
        if (!results.Any())
            return new BasicStatistics();

        var sortedTimes = results.Select(r => r.ResponseTimeMs).OrderBy(t => t).ToList();
        return new BasicStatistics
        {
            SuccessCount = results.Count(r => r.IsSuccess),
            FailureCount = results.Count - results.Count(r => r.IsSuccess),
            AvgTime = results.Average(r => r.ResponseTimeMs),
            Percentile95 = CalculatePercentile(sortedTimes, 0.95)
        };
    }

    private UrlStatistics CalculateUrlStatistics(List<TestResult> results)
    {
        if (!results.Any())
            return new UrlStatistics();

        var sortedTimes = results.Select(r => r.ResponseTimeMs).OrderBy(t => t).ToList();
        return new UrlStatistics
        {
            SuccessCount = results.Count(r => r.IsSuccess),
            FailureCount = results.Count - results.Count(r => r.IsSuccess),
            AvgTime = results.Average(r => r.ResponseTimeMs),
            Percentile95 = CalculatePercentile(sortedTimes, 0.95),
            MinTime = sortedTimes.FirstOrDefault(),
            MaxTime = sortedTimes.LastOrDefault()
        };
    }

    private TimeSpan CalculateDuration(List<TestResult> results)
    {
        if (!results.Any()) return TimeSpan.Zero;
        var startTime = results.Min(r => r.Timestamp);
        var endTime = results.Max(r => r.Timestamp);
        return endTime - startTime;
    }

    private double CalculateRps(int requestCount, TimeSpan duration)
    {
        var totalSeconds = duration.TotalSeconds;
        return totalSeconds > 0 ? requestCount / totalSeconds : 0;
    }

    private string GetStatusCodeColor(int statusCode)
    {
        return statusCode >= 200 && statusCode < 300 ? "green" :
               statusCode >= 300 && statusCode < 400 ? "yellow" : "red";
    }

    private string TruncateUrl(string url, int maxLength)
    {
        return url.Length > maxLength ? url.Substring(0, maxLength - 3) + "..." : url;
    }

    public Table CreateUrlStatsTable(List<TestResult> results, List<string> urls, DateTime startTime)
    {
        var table = new Table();
        table.AddColumn("URL");
        table.AddColumn("Запросов");
        table.AddColumn("Успешных");
        table.AddColumn("Неуспешных");
        table.AddColumn("Среднее (мс)");
        table.AddColumn("Мин/Макс (мс)");
        table.AddColumn("RPS");
        table.Border = TableBorder.Rounded;
        
        var elapsedSeconds = (DateTime.UtcNow - startTime).TotalSeconds;
        var totalRps = elapsedSeconds > 0 ? results.Count / elapsedSeconds : 0;
        
        table.Title = new TableTitle($"[bold cyan]Статистика (в реальном времени) | Общий RPS: {totalRps:F2}[/]");

        foreach (var url in urls)
        {
            var urlResults = results.Where(r => r.Url == url).ToList();
            var stats = CalculateUrlStatistics(urlResults);
            var urlRps = elapsedSeconds > 0 ? urlResults.Count / elapsedSeconds : 0;

            table.AddRow(
                TruncateUrl(url, 40),
                urlResults.Count.ToString(),
                $"[green]{stats.SuccessCount}[/]",
                stats.FailureCount > 0 ? $"[red]{stats.FailureCount}[/]" : "0",
                urlResults.Any() ? $"{stats.AvgTime:F1}" : "-",
                urlResults.Any() ? $"{stats.MinTime}/{stats.MaxTime}" : "-/-",
                $"{urlRps:F2}"
            );
        }

        return table;
    }

    // Вспомогательные классы для статистики
    private class BasicStatistics
    {
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public double AvgTime { get; set; }
        public long Percentile95 { get; set; }
    }

    private class UrlStatistics
    {
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public double AvgTime { get; set; }
        public long Percentile95 { get; set; }
        public long MinTime { get; set; }
        public long MaxTime { get; set; }
    }
}
