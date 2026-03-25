using Spectre.Console;
using ConsoleLoadTesting.Models;

namespace ConsoleLoadTesting.Services;

public sealed class ResultService
{
    #region Constants
    private const int DefaultTopErrorsCount = 10;
    #endregion

    #region Public API

    public void DisplayResultsFromFile(string filePath, int chartTimeStepSeconds = 1)
    {
        var analysis = AnalyzeFile(filePath, chartTimeStepSeconds, includeCharts: true);
        if (!analysis.HasResults)
        {
            DisplayNoDataMessage();
            return;
        }

        DisplayAnalyzedResults(analysis);
    }

    public void DisplaySummaryReport(IEnumerable<string> filePaths, int chartTimeStepSeconds = 1)
    {
        DisplaySummaryReportHeader();

        var summaries = new List<FileAnalysisSummary>();
        var combined = CreateAnalysis(chartTimeStepSeconds);

        foreach (var filePath in filePaths)
        {
            var analysis = AnalyzeFile(filePath, chartTimeStepSeconds, includeCharts: false);
            AddSummary(summaries, combined, filePath, analysis);
        }

        DisplayPreparedSummaryReport(
            summaries,
            combined,
            () => PopulateChartsForFiles(summaries.Select(summary => summary.FilePath), combined, combined.ChartTimeStepSeconds));
    }

    public void DisplaySummaryReport(Dictionary<string, List<TestResult>> testResultsDict, int chartTimeStepSeconds = 1)
    {
        DisplaySummaryReportHeader();

        var summaries = new List<FileAnalysisSummary>();
        var combined = CreateAnalysis(chartTimeStepSeconds);

        foreach (var entry in testResultsDict)
        {
            var analysis = AnalyzeResults(entry.Value, chartTimeStepSeconds, includeCharts: false);
            AddSummary(summaries, combined, entry.Key, analysis);
        }

        DisplayPreparedSummaryReport(
            summaries,
            combined,
            () => PopulateChartsForResults(testResultsDict.Values.SelectMany(results => results), combined, combined.ChartTimeStepSeconds));
    }

    public Table CreateUrlStatsTable(RealtimeStats stats, List<string> urls, DateTime startTime)
    {
        var table = new Table();
        table.AddColumn("URL");
        table.AddColumn("Requests");
        table.AddColumn("Success");
        table.AddColumn("Failures");
        table.AddColumn("Avg (ms)");
        table.AddColumn("Min/Max (ms)");
        table.AddColumn("RPS");
        table.Border = TableBorder.Rounded;

        var elapsedSeconds = (DateTime.UtcNow - startTime).TotalSeconds;
        var totalRps = elapsedSeconds > 0 ? stats.TotalRequests / elapsedSeconds : 0;

        table.Title = new TableTitle($"[bold cyan]Realtime stats | Total RPS: {totalRps:F2}[/]");

        foreach (var url in urls)
        {
            var urlStats = stats.GetUrlStats(url);
            var urlRps = elapsedSeconds > 0 ? urlStats.TotalRequests / elapsedSeconds : 0;

            table.AddRow(
                TruncateUrl(url, 40),
                urlStats.TotalRequests.ToString(),
                $"[green]{urlStats.SuccessCount}[/]",
                urlStats.FailureCount > 0 ? $"[red]{urlStats.FailureCount}[/]" : "0",
                urlStats.TotalRequests > 0 ? $"{urlStats.AverageResponseTimeMs:F1}" : "-",
                urlStats.TotalRequests > 0 ? $"{urlStats.MinTimeMs}/{urlStats.MaxTimeMs}" : "-/-",
                $"{urlRps:F2}"
            );
        }

        return table;
    }

    #endregion

    #region Summary report helpers

    private static void DisplaySummaryReportHeader()
    {
        AnsiConsole.MarkupLine("[bold cyan]Summary report[/]");
        AnsiConsole.WriteLine();
    }

    private static void DisplayNoDataMessage()
    {
        AnsiConsole.MarkupLine("[yellow]No data to display[/]");
    }

    private static void AddSummary(
        List<FileAnalysisSummary> summaries,
        AnalyzedResults combined,
        string sourceName,
        AnalyzedResults analysis)
    {
        if (!analysis.HasResults)
        {
            return;
        }

        summaries.Add(new FileAnalysisSummary(sourceName, analysis));
        combined.MergeFrom(analysis);
    }

    private void DisplayPreparedSummaryReport(
        List<FileAnalysisSummary> summaries,
        AnalyzedResults combined,
        Action populateCharts)
    {
        if (summaries.Count == 0)
        {
            DisplayNoDataMessage();
            return;
        }

        populateCharts();
        DisplayTestsSummaryTable(summaries);
        DisplayOverallStatisticsTable(combined);
        DisplaySummaryUrlStatisticsTable(combined);
        DisplayErrorUrlsTable(combined);
        DisplayTopErrorsTable(combined, DefaultTopErrorsCount);

        if (combined.HasResults)
        {
            DisplayTimeCharts(combined, combined.ChartTimeStepSeconds);
        }
    }

    #endregion

    #region Display (tables/charts)

    private void DisplayAnalyzedResults(AnalyzedResults analysis)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Load test results[/]");
        AnsiConsole.WriteLine();

        DisplaySuccessFailureChart(analysis);
        DisplayTimeStatistics(analysis);
        DisplayStatusCodesTable(analysis);
        DisplayUrlStatisticsTable(analysis);
        DisplayErrorUrlsTable(analysis);
        DisplayTopErrorsTable(analysis, DefaultTopErrorsCount);

        if (analysis.HasResults)
        {
            AnsiConsole.WriteLine();
            DisplayTimeCharts(analysis, analysis.ChartTimeStepSeconds);
        }
    }

    private void DisplaySuccessFailureChart(AnalyzedResults analysis)
    {
        var chart = new BarChart()
            .Width(60)
            .Label("[green]Successful/Failed requests[/]")
            .AddItem("Successful", analysis.SuccessCount, Color.Green)
            .AddItem("Failed", analysis.FailureCount, Color.Red);

        AnsiConsole.Write(chart);
        AnsiConsole.WriteLine();
    }

    private void DisplayTimeStatistics(AnalyzedResults analysis)
    {
        if (!analysis.HasResults)
        {
            return;
        }

        AnsiConsole.MarkupLine($"[bold]Average request time:[/] {analysis.AverageResponseTime:F2} ms");
        AnsiConsole.MarkupLine($"[bold]95th percentile:[/] {analysis.Percentile95} ms");
        AnsiConsole.WriteLine();
    }

    private void DisplayStatusCodesTable(AnalyzedResults analysis)
    {
        if (analysis.StatusCodeCounts.Count == 0)
        {
            return;
        }

        AnsiConsole.MarkupLine("[bold]Response status codes:[/]");
        var statusTable = new Table();
        statusTable.AddColumn("Status");
        statusTable.AddColumn("Count");

        foreach (var pair in analysis.StatusCodeCounts.OrderBy(pair => pair.Key))
        {
            var color = GetStatusCodeColor(pair.Key);
            statusTable.AddRow($"[{color}]{pair.Key}[/]", pair.Value.ToString());
        }

        AnsiConsole.Write(statusTable);
        AnsiConsole.WriteLine();
    }

    private void DisplayUrlStatisticsTable(AnalyzedResults analysis)
    {
        if (analysis.UrlStats.Count <= 1)
        {
            return;
        }

        AnsiConsole.MarkupLine("[bold]Per-URL statistics:[/]");
        AnsiConsole.WriteLine();

        var urlStatsTable = new Table();
        urlStatsTable.AddColumn("URL");
        urlStatsTable.AddColumn("Total");
        urlStatsTable.AddColumn("Success");
        urlStatsTable.AddColumn("Failures");
        urlStatsTable.AddColumn("Avg (ms)");
        urlStatsTable.AddColumn("95p (ms)");
        urlStatsTable.AddColumn("Min/Max (ms)");

        foreach (var pair in analysis.UrlStats.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var url = pair.Key;
            var stats = pair.Value;

            urlStatsTable.AddRow(
                TruncateUrl(url, 50),
                stats.TotalRequests.ToString(),
                $"[green]{stats.SuccessCount}[/]",
                stats.FailureCount > 0 ? $"[red]{stats.FailureCount}[/]" : "0",
                $"{stats.AverageResponseTime:F2}",
                stats.Percentile95.ToString(),
                $"{stats.MinTime}/{stats.MaxTime}"
            );
        }

        AnsiConsole.Write(urlStatsTable);
        AnsiConsole.WriteLine();
    }

    private void DisplayErrorUrlsTable(AnalyzedResults analysis)
    {
        if (analysis.ErrorUrls.Count == 0)
        {
            return;
        }

        AnsiConsole.MarkupLine("[bold red]URLs with errors:[/]");
        var errorTable = new Table();
        errorTable.AddColumn("URL");
        errorTable.AddColumn("Error count");
        errorTable.AddColumn("Last error");

        foreach (var pair in analysis.ErrorUrls.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            errorTable.AddRow(
                pair.Key,
                pair.Value.Count.ToString(),
                pair.Value.LastError
            );
        }

        AnsiConsole.Write(errorTable);
    }

    private void DisplayTopErrorsTable(AnalyzedResults analysis, int topN)
    {
        if (analysis.ErrorMessageCounts.Count == 0)
        {
            return;
        }

        var items = analysis.ErrorMessageCounts
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Take(Math.Max(1, topN))
            .ToList();

        if (items.Count == 0)
        {
            return;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold red]Top errors:[/]");

        var table = new Table();
        table.AddColumn("Error");
        table.AddColumn("Count");
        table.Border = TableBorder.Rounded;

        foreach (var item in items)
        {
            table.AddRow(TruncateForTable(item.Key, 120), item.Value.ToString());
        }

        AnsiConsole.Write(table);
    }

    private void DisplayTestsSummaryTable(List<FileAnalysisSummary> summaries)
    {
        var summaryTable = new Table();
        summaryTable.AddColumn("File");
        summaryTable.AddColumn("Duration");
        summaryTable.AddColumn("Requests");
        summaryTable.AddColumn("Success");
        summaryTable.AddColumn("Failures");
        summaryTable.AddColumn("Avg (ms)");
        summaryTable.AddColumn("95p");
        summaryTable.AddColumn("RPS");
        summaryTable.Border = TableBorder.Rounded;
        summaryTable.Title = new TableTitle("[bold]Tests summary[/]");

        foreach (var summary in summaries)
        {
            var analysis = summary.Analysis;
            if (!analysis.HasResults)
            {
                continue;
            }

            var duration = analysis.Duration;
            var rps = CalculateRps(analysis.TotalRequests, duration);

            summaryTable.AddRow(
                Path.GetFileName(summary.FilePath),
                duration.ToString(@"mm\:ss"),
                analysis.TotalRequests.ToString(),
                $"[green]{analysis.SuccessCount}[/]",
                analysis.FailureCount > 0 ? $"[red]{analysis.FailureCount}[/]" : "0",
                $"{analysis.AverageResponseTime:F1}",
                analysis.Percentile95.ToString(),
                $"{rps:F2}"
            );
        }

        AnsiConsole.Write(summaryTable);
        AnsiConsole.WriteLine();
    }

    private void DisplayOverallStatisticsTable(AnalyzedResults analysis)
    {
        var duration = analysis.Duration;
        var rps = CalculateRps(analysis.TotalRequests, duration);

        AnsiConsole.MarkupLine("[bold]Overall statistics (all tests):[/]");
        AnsiConsole.WriteLine();

        var overallTable = new Table();
        overallTable.AddColumn("Metric");
        overallTable.AddColumn("Value");
        overallTable.Border = TableBorder.Rounded;

        overallTable.AddRow("Total requests", analysis.TotalRequests.ToString());
        overallTable.AddRow("Successful", $"[green]{analysis.SuccessCount}[/]");
        overallTable.AddRow("Failed", analysis.FailureCount > 0 ? $"[red]{analysis.FailureCount}[/]" : "0");
        overallTable.AddRow("Average time (ms)", $"{analysis.AverageResponseTime:F2}");
        overallTable.AddRow("95th percentile (ms)", analysis.Percentile95.ToString());
        overallTable.AddRow("Total RPS", $"{rps:F2}");

        AnsiConsole.Write(overallTable);
        AnsiConsole.WriteLine();
    }

    private void DisplaySummaryUrlStatisticsTable(AnalyzedResults analysis)
    {
        if (analysis.UrlStats.Count <= 1)
        {
            return;
        }

        AnsiConsole.MarkupLine("[bold]Per-URL statistics:[/]");
        AnsiConsole.WriteLine();

        var urlTable = new Table();
        urlTable.AddColumn("URL");
        urlTable.AddColumn("Total");
        urlTable.AddColumn("Success");
        urlTable.AddColumn("Failures");
        urlTable.AddColumn("Avg (ms)");
        urlTable.AddColumn("95p (ms)");
        urlTable.Border = TableBorder.Rounded;

        foreach (var pair in analysis.UrlStats.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var url = pair.Key;
            var stats = pair.Value;

            urlTable.AddRow(
                TruncateUrl(url, 50),
                stats.TotalRequests.ToString(),
                $"[green]{stats.SuccessCount}[/]",
                stats.FailureCount > 0 ? $"[red]{stats.FailureCount}[/]" : "0",
                $"{stats.AverageResponseTime:F2}",
                stats.Percentile95.ToString()
            );
        }

        AnsiConsole.Write(urlTable);
        AnsiConsole.WriteLine();
    }

    private void DisplayTimeCharts(AnalyzedResults analysis, int stepSeconds)
    {
        if (!analysis.HasResults || analysis.RequestsByInterval.Count == 0 || analysis.StartTime is null)
        {
            return;
        }

        AnsiConsole.MarkupLine("[bold]Charts over time:[/]");
        AnsiConsole.WriteLine();

        var chartTable = new Table();
        chartTable.AddColumn("Time");
        chartTable.AddColumn("RPS");
        chartTable.AddColumn("Errors");
        chartTable.Border = TableBorder.Rounded;
        chartTable.ShowHeaders = true;

        var maxRequests = analysis.RequestsByInterval.Values.DefaultIfEmpty(1).Max();
        var maxErrors = analysis.ErrorsByInterval.Values.DefaultIfEmpty(1).Max();
        var allIntervals = analysis.RequestsByInterval.Keys
            .Union(analysis.ErrorsByInterval.Keys)
            .OrderBy(interval => interval)
            .ToList();

        foreach (var interval in allIntervals)
        {
            var requestCount = analysis.RequestsByInterval.GetValueOrDefault(interval, 0);
            var errorCount = analysis.ErrorsByInterval.GetValueOrDefault(interval, 0);

            var requestsBar = CreateBar(requestCount, maxRequests, 35, Color.Blue);
            var errorsBar = CreateBar(errorCount, maxErrors, 35, Color.Red);
            var timeLabel = analysis.StartTime.Value.AddSeconds(interval * stepSeconds).ToString("HH:mm:ss");

            chartTable.AddRow(
                timeLabel,
                $"{requestsBar} [blue]{requestCount}[/]",
                $"{errorsBar} [red]{errorCount}[/]"
            );
        }

        AnsiConsole.Write(chartTable);
        AnsiConsole.WriteLine();
    }

    #endregion

    #region Analysis & aggregation

    private AnalyzedResults AnalyzeResults(IEnumerable<TestResult> results, int chartTimeStepSeconds, bool includeCharts)
    {
        var materializedResults = results as IReadOnlyCollection<TestResult> ?? results.ToList();
        var analysis = CreateAnalysis(chartTimeStepSeconds);

        foreach (var result in materializedResults)
        {
            analysis.AddResult(result, CalculatePercentile);
        }

        if (includeCharts)
        {
            PopulateChartsForResults(materializedResults, analysis, analysis.ChartTimeStepSeconds);
        }

        return analysis;
    }

    private AnalyzedResults AnalyzeFile(string filePath, int chartTimeStepSeconds, bool includeCharts)
    {
        var analysis = CreateAnalysis(chartTimeStepSeconds);

        foreach (var result in EnumerateResultsFromFile(filePath))
        {
            analysis.AddResult(result, CalculatePercentile);
        }

        if (includeCharts && analysis.HasResults)
        {
            PopulateChartsForFiles(new[] { filePath }, analysis, analysis.ChartTimeStepSeconds);
        }

        return analysis;
    }

    #endregion

    #region Time charts

    private void PopulateChartsForFiles(IEnumerable<string> filePaths, AnalyzedResults analysis, int stepSeconds)
    {
        if (!analysis.HasResults || analysis.StartTime is null)
        {
            return;
        }

        var normalizedStep = NormalizeStep(stepSeconds);

        foreach (var filePath in filePaths)
        {
            foreach (var result in EnumerateResultsFromFile(filePath))
            {
                AddChartPoint(analysis, result, normalizedStep);
            }
        }
    }

    private void PopulateChartsForResults(IEnumerable<TestResult> results, AnalyzedResults analysis, int stepSeconds)
    {
        if (!analysis.HasResults || analysis.StartTime is null)
        {
            return;
        }

        var normalizedStep = NormalizeStep(stepSeconds);

        foreach (var result in results)
        {
            AddChartPoint(analysis, result, normalizedStep);
        }
    }

    private void AddChartPoint(AnalyzedResults analysis, TestResult result, int stepSeconds)
    {
        if (analysis.StartTime is null)
        {
            return;
        }

        var interval = GetTimeInterval(result.Timestamp, analysis.StartTime.Value, stepSeconds);
        analysis.RequestsByInterval[interval] = analysis.RequestsByInterval.GetValueOrDefault(interval, 0) + 1;

        if (!result.IsSuccess)
        {
            analysis.ErrorsByInterval[interval] = analysis.ErrorsByInterval.GetValueOrDefault(interval, 0) + 1;
        }
    }

    #endregion

    #region Internal helpers

    private static AnalyzedResults CreateAnalysis(int chartTimeStepSeconds)
    {
        return new AnalyzedResults
        {
            ChartTimeStepSeconds = NormalizeStep(chartTimeStepSeconds)
        };
    }

    private static IEnumerable<TestResult> EnumerateResultsFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}", filePath);
        }

        using var reader = FileService.CreateSequentialReader(filePath);
        var header = reader.ReadLine();
        if (header is null)
        {
            throw new InvalidDataException($"Empty file: {filePath}");
        }

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (TestResultCsv.TryParse(line, out var result) && result is not null)
            {
                yield return result;
            }
        }
    }

    private static int GetTimeInterval(DateTime timestamp, DateTime startTime, int stepSeconds)
    {
        var seconds = (int)(timestamp - startTime).TotalSeconds;
        return Math.Max(0, seconds / stepSeconds);
    }

    private static string CreateBar(int value, int maxValue, int width, Color color)
    {
        if (maxValue == 0)
        {
            return new string(' ', width);
        }

        var barWidth = (int)((double)value / maxValue * width);
        var bar = new string('█', barWidth);
        return $"[{color}]{bar}[/]";
    }

    private static long CalculatePercentile(SortedDictionary<long, long> histogram, int totalCount, double percentile)
    {
        if (histogram.Count == 0 || totalCount <= 0)
        {
            return 0;
        }

        var targetIndex = Math.Max(1, (int)Math.Ceiling(totalCount * percentile));
        long accumulated = 0;

        foreach (var entry in histogram)
        {
            accumulated += entry.Value;
            if (accumulated >= targetIndex)
            {
                return entry.Key;
            }
        }

        return histogram.Last().Key;
    }

    private static double CalculateRps(int requestCount, TimeSpan duration)
    {
        var totalSeconds = duration.TotalSeconds;
        return totalSeconds > 0 ? requestCount / totalSeconds : 0;
    }

    private static string GetStatusCodeColor(int statusCode)
    {
        return statusCode >= 200 && statusCode < 300 ? "green" :
               statusCode >= 300 && statusCode < 400 ? "yellow" : "red";
    }

    private static string TruncateUrl(string url, int maxLength)
    {
        return url.Length > maxLength ? url.Substring(0, maxLength - 3) + "..." : url;
    }

    private static string TruncateForTable(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value.Length > maxLength ? value.Substring(0, maxLength - 3) + "..." : value;
    }

    private static int NormalizeStep(int stepSeconds)
    {
        return stepSeconds > 0 ? stepSeconds : 1;
    }

    #endregion

    #region Nested types

    private sealed class FileAnalysisSummary
    {
        public FileAnalysisSummary(string filePath, AnalyzedResults analysis)
        {
            FilePath = filePath;
            Analysis = analysis;
        }

        public string FilePath { get; }
        public AnalyzedResults Analysis { get; }
    }

    private sealed class AnalyzedResults
    {
        private Func<SortedDictionary<long, long>, int, double, long> _percentileCalculator = (_, _, _) => 0;

        public int TotalRequests { get; private set; }
        public int SuccessCount { get; private set; }
        public int FailureCount { get; private set; }
        public long TotalResponseTimeMs { get; private set; }
        public SortedDictionary<long, long> ResponseTimeHistogram { get; } = new();
        public Dictionary<int, int> StatusCodeCounts { get; } = new();
        public Dictionary<string, UrlStatisticsAccumulator> UrlStats { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, ErrorUrlAccumulator> ErrorUrls { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, int> ErrorMessageCounts { get; } = new(StringComparer.Ordinal);
        public Dictionary<int, int> RequestsByInterval { get; } = new();
        public Dictionary<int, int> ErrorsByInterval { get; } = new();
        public DateTime? StartTime { get; private set; }
        public DateTime? EndTime { get; private set; }
        public int ChartTimeStepSeconds { get; set; } = 1;

        public bool HasResults => TotalRequests > 0;
        public double AverageResponseTime => TotalRequests > 0 ? (double)TotalResponseTimeMs / TotalRequests : 0;
        public long Percentile95 => TotalRequests > 0 ? _percentileCalculator(ResponseTimeHistogram, TotalRequests, 0.95) : 0;
        public TimeSpan Duration => StartTime.HasValue && EndTime.HasValue ? EndTime.Value - StartTime.Value : TimeSpan.Zero;

        public void AddResult(TestResult result, Func<SortedDictionary<long, long>, int, double, long> percentileCalculator)
        {
            _percentileCalculator = percentileCalculator;
            TotalRequests++;
            TotalResponseTimeMs += result.ResponseTimeMs;

            if (result.IsSuccess)
            {
                SuccessCount++;
            }
            else
            {
                FailureCount++;
            }

            ResponseTimeHistogram[result.ResponseTimeMs] = ResponseTimeHistogram.GetValueOrDefault(result.ResponseTimeMs) + 1;

            if (result.StatusCode > 0)
            {
                StatusCodeCounts[result.StatusCode] = StatusCodeCounts.GetValueOrDefault(result.StatusCode) + 1;
            }

            if (StartTime is null || result.Timestamp < StartTime.Value)
            {
                StartTime = result.Timestamp;
            }

            if (EndTime is null || result.Timestamp > EndTime.Value)
            {
                EndTime = result.Timestamp;
            }

            if (!UrlStats.TryGetValue(result.Url, out var urlStats))
            {
                urlStats = new UrlStatisticsAccumulator();
                UrlStats[result.Url] = urlStats;
            }

            urlStats.Add(result, percentileCalculator);

            if (!result.IsSuccess)
            {
                var lastError = string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? $"HTTP {result.StatusCode}"
                    : result.ErrorMessage!;

                if (!ErrorUrls.TryGetValue(result.Url, out var errorInfo))
                {
                    errorInfo = new ErrorUrlAccumulator();
                    ErrorUrls[result.Url] = errorInfo;
                }

                errorInfo.Count++;
                errorInfo.LastError = lastError;

                ErrorMessageCounts[lastError] = ErrorMessageCounts.GetValueOrDefault(lastError) + 1;
            }
        }

        public void MergeFrom(AnalyzedResults other)
        {
            _percentileCalculator = other._percentileCalculator;
            ChartTimeStepSeconds = other.ChartTimeStepSeconds;
            TotalRequests += other.TotalRequests;
            SuccessCount += other.SuccessCount;
            FailureCount += other.FailureCount;
            TotalResponseTimeMs += other.TotalResponseTimeMs;

            foreach (var pair in other.ResponseTimeHistogram)
            {
                ResponseTimeHistogram[pair.Key] = ResponseTimeHistogram.GetValueOrDefault(pair.Key) + pair.Value;
            }

            foreach (var pair in other.StatusCodeCounts)
            {
                StatusCodeCounts[pair.Key] = StatusCodeCounts.GetValueOrDefault(pair.Key) + pair.Value;
            }

            foreach (var pair in other.UrlStats)
            {
                if (!UrlStats.TryGetValue(pair.Key, out var urlStats))
                {
                    urlStats = new UrlStatisticsAccumulator();
                    UrlStats[pair.Key] = urlStats;
                }

                urlStats.MergeFrom(pair.Value, _percentileCalculator);
            }

            foreach (var pair in other.ErrorUrls)
            {
                if (!ErrorUrls.TryGetValue(pair.Key, out var errorInfo))
                {
                    errorInfo = new ErrorUrlAccumulator();
                    ErrorUrls[pair.Key] = errorInfo;
                }

                errorInfo.Count += pair.Value.Count;
                errorInfo.LastError = pair.Value.LastError;
            }

            foreach (var pair in other.ErrorMessageCounts)
            {
                ErrorMessageCounts[pair.Key] = ErrorMessageCounts.GetValueOrDefault(pair.Key) + pair.Value;
            }

            if (other.StartTime.HasValue && (!StartTime.HasValue || other.StartTime.Value < StartTime.Value))
            {
                StartTime = other.StartTime;
            }

            if (other.EndTime.HasValue && (!EndTime.HasValue || other.EndTime.Value > EndTime.Value))
            {
                EndTime = other.EndTime;
            }
        }
    }

    private sealed class UrlStatisticsAccumulator
    {
        private Func<SortedDictionary<long, long>, int, double, long> _percentileCalculator = (_, _, _) => 0;

        public int TotalRequests { get; private set; }
        public int SuccessCount { get; private set; }
        public int FailureCount { get; private set; }
        public long TotalResponseTimeMs { get; private set; }
        public SortedDictionary<long, long> ResponseTimeHistogram { get; } = new();
        public long MinTime { get; private set; } = long.MaxValue;
        public long MaxTime { get; private set; } = long.MinValue;

        public double AverageResponseTime => TotalRequests > 0 ? (double)TotalResponseTimeMs / TotalRequests : 0;
        public long Percentile95 => TotalRequests > 0 ? _percentileCalculator(ResponseTimeHistogram, TotalRequests, 0.95) : 0;

        public void Add(TestResult result, Func<SortedDictionary<long, long>, int, double, long> percentileCalculator)
        {
            _percentileCalculator = percentileCalculator;
            TotalRequests++;
            TotalResponseTimeMs += result.ResponseTimeMs;

            if (result.IsSuccess)
            {
                SuccessCount++;
            }
            else
            {
                FailureCount++;
            }

            ResponseTimeHistogram[result.ResponseTimeMs] = ResponseTimeHistogram.GetValueOrDefault(result.ResponseTimeMs) + 1;
            MinTime = Math.Min(MinTime, result.ResponseTimeMs);
            MaxTime = Math.Max(MaxTime, result.ResponseTimeMs);
        }

        public void MergeFrom(UrlStatisticsAccumulator other, Func<SortedDictionary<long, long>, int, double, long> percentileCalculator)
        {
            var currentTotal = TotalRequests;

            _percentileCalculator = percentileCalculator;
            TotalRequests += other.TotalRequests;
            SuccessCount += other.SuccessCount;
            FailureCount += other.FailureCount;
            TotalResponseTimeMs += other.TotalResponseTimeMs;

            foreach (var pair in other.ResponseTimeHistogram)
            {
                ResponseTimeHistogram[pair.Key] = ResponseTimeHistogram.GetValueOrDefault(pair.Key) + pair.Value;
            }

            if (other.TotalRequests > 0)
            {
                MinTime = currentTotal == 0 ? other.MinTime : Math.Min(MinTime, other.MinTime);
                MaxTime = currentTotal == 0 ? other.MaxTime : Math.Max(MaxTime, other.MaxTime);
            }
        }
    }

    private sealed class ErrorUrlAccumulator
    {
        public int Count { get; set; }
        public string LastError { get; set; } = string.Empty;
    }

    #endregion
}
