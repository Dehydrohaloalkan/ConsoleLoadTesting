namespace ConsoleLoadTesting.Models;

public class TestConfig
{
    public List<string> Urls { get; set; } = new();
    public UrlMode UrlMode { get; set; } = UrlMode.Sequential;
    public int VirtualUsers { get; set; } = 1;
    public int RequestCount { get; set; } = 1;
    public int DelayMs { get; set; } = 0;
    public int MaxInFlightPerUser { get; set; } = 1;
    public Dictionary<string, string> Headers { get; set; } = new();
    public int ChartTimeStepSeconds { get; set; } = 1;
}

public enum UrlMode
{
    Sequential,
    Random
}
