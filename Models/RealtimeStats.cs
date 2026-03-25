namespace ConsoleLoadTesting.Models;

public class RealtimeStats
{
    private readonly Dictionary<string, RealtimeUrlStats> _urlStats = new(StringComparer.Ordinal);

    public int TotalRequests { get; private set; }
    public int SuccessCount { get; private set; }
    public int FailureCount { get; private set; }

    public void Add(TestResult result)
    {
        TotalRequests++;

        if (result.IsSuccess)
        {
            SuccessCount++;
        }
        else
        {
            FailureCount++;
        }

        if (!_urlStats.TryGetValue(result.Url, out var urlStats))
        {
            urlStats = new RealtimeUrlStats();
            _urlStats[result.Url] = urlStats;
        }

        urlStats.Add(result);
    }

    public RealtimeUrlStats GetUrlStats(string url)
    {
        return _urlStats.TryGetValue(url, out var stats) ? stats : RealtimeUrlStats.Empty;
    }
}

public class RealtimeUrlStats
{
    public static RealtimeUrlStats Empty { get; } = new();

    public int TotalRequests { get; private set; }
    public int SuccessCount { get; private set; }
    public int FailureCount { get; private set; }
    public long TotalResponseTimeMs { get; private set; }
    public long MinTimeMs { get; private set; } = long.MaxValue;
    public long MaxTimeMs { get; private set; } = long.MinValue;

    public double AverageResponseTimeMs => TotalRequests > 0
        ? (double)TotalResponseTimeMs / TotalRequests
        : 0;

    public void Add(TestResult result)
    {
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

        if (result.ResponseTimeMs < MinTimeMs)
        {
            MinTimeMs = result.ResponseTimeMs;
        }

        if (result.ResponseTimeMs > MaxTimeMs)
        {
            MaxTimeMs = result.ResponseTimeMs;
        }
    }
}
