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
