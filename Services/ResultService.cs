using Spectre.Console;
using ConsoleLoadTesting.Models;

namespace ConsoleLoadTesting.Services;

public class ResultService
{
    public void DisplayResults(List<TestResult> results)
    {
        AnsiConsole.WriteLine();

        // Статистика успешных/неуспешных запросов
        var successCount = results.Count(r => r.IsSuccess);
        var failureCount = results.Count - successCount;

        AnsiConsole.MarkupLine("[bold]Результаты нагрузочного тестирования[/]");
        AnsiConsole.WriteLine();

        // График успешных/неуспешных запросов
        var chart = new BarChart()
            .Width(60)
            .Label("[green]Успешные/Неуспешные запросы[/]")
            .AddItem("Успешные", successCount, Color.Green)
            .AddItem("Неуспешные", failureCount, Color.Red);

        AnsiConsole.Write(chart);
        AnsiConsole.WriteLine();

        // Среднее время запроса
        var avgTime = results.Average(r => r.ResponseTimeMs);
        AnsiConsole.MarkupLine($"[bold]Среднее время запроса:[/] {avgTime:F2} мс");

        // 95 процентиль
        var sortedTimes = results.Select(r => r.ResponseTimeMs).OrderBy(t => t).ToList();
        var percentile95Index = (int)(sortedTimes.Count * 0.95);
        var percentile95 = percentile95Index < sortedTimes.Count 
            ? sortedTimes[percentile95Index] 
            : sortedTimes.LastOrDefault();
        AnsiConsole.MarkupLine($"[bold]95 процентиль по времени:[/] {percentile95} мс");
        AnsiConsole.WriteLine();

        // Статусы ответов
        var statusGroups = results
            .Where(r => r.StatusCode > 0)
            .GroupBy(r => r.StatusCode)
            .OrderBy(g => g.Key)
            .ToList();

        if (statusGroups.Any())
        {
            AnsiConsole.MarkupLine("[bold]Статусы ответов:[/]");
            var statusTable = new Table();
            statusTable.AddColumn("Статус");
            statusTable.AddColumn("Количество");

            foreach (var group in statusGroups)
            {
                var color = group.Key >= 200 && group.Key < 300 ? "green" :
                           group.Key >= 300 && group.Key < 400 ? "yellow" : "red";
                statusTable.AddRow(
                    $"[{color}]{group.Key}[/]",
                    group.Count().ToString()
                );
            }

            AnsiConsole.Write(statusTable);
            AnsiConsole.WriteLine();
        }

        // Статистика по каждой ссылке (если ссылок несколько)
        var uniqueUrls = results.Select(r => r.Url).Distinct().ToList();
        if (uniqueUrls.Count > 1)
        {
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
                var urlSuccessCount = urlResults.Count(r => r.IsSuccess);
                var urlFailureCount = urlResults.Count - urlSuccessCount;
                var urlAvgTime = urlResults.Average(r => r.ResponseTimeMs);
                
                var urlSortedTimes = urlResults.Select(r => r.ResponseTimeMs).OrderBy(t => t).ToList();
                var urlPercentile95Index = (int)(urlSortedTimes.Count * 0.95);
                var urlPercentile95 = urlPercentile95Index < urlSortedTimes.Count 
                    ? urlSortedTimes[urlPercentile95Index] 
                    : urlSortedTimes.LastOrDefault();
                
                var urlMinTime = urlSortedTimes.FirstOrDefault();
                var urlMaxTime = urlSortedTimes.LastOrDefault();

                // Обрезаем длинный URL для отображения
                var displayUrl = url.Length > 50 ? url.Substring(0, 47) + "..." : url;

                urlStatsTable.AddRow(
                    displayUrl,
                    urlResults.Count.ToString(),
                    $"[green]{urlSuccessCount}[/]",
                    urlFailureCount > 0 ? $"[red]{urlFailureCount}[/]" : "0",
                    $"{urlAvgTime:F2}",
                    urlPercentile95.ToString(),
                    $"{urlMinTime}/{urlMaxTime}"
                );
            }

            AnsiConsole.Write(urlStatsTable);
            AnsiConsole.WriteLine();
        }

        // Ссылки с ошибками
        var errorUrls = results
            .Where(r => !r.IsSuccess)
            .GroupBy(r => r.Url)
            .ToList();

        if (errorUrls.Any())
        {
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
    }

    public void SaveResultsToFile(List<TestResult> results, string filePath)
    {
        var lines = new List<string> { "UserId,Url,Status,TimeMs" };
        
        foreach (var result in results)
        {
            lines.Add($"{result.UserId},{EscapeCsv(result.Url)},{result.StatusCode},{result.ResponseTimeMs}");
        }

        // Создаём директорию, если её нет
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
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
}
