namespace ConsoleLoadTesting.Models;

public class TestResult
{
    public int UserId { get; set; }
    public string Url { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public long ResponseTimeMs { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
}
