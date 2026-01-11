using ConsoleLoadTesting.Models;
using ConsoleLoadTesting.Services;
using Xunit;

namespace ConsoleLoadTesting.Tests;

public class LoadTestServiceTests : IDisposable
{
    private readonly LoadTestService _loadTestService;

    public LoadTestServiceTests()
    {
        _loadTestService = new LoadTestService();
    }

    [Fact]
    public async Task RunLoadTestAsync_ShouldReturnResults_WithSequentialMode()
    {
        // Arrange
        var config = new TestConfig
        {
            Urls = new List<string> { "https://httpbin.org/get", "https://httpbin.org/status/200" },
            UrlMode = UrlMode.Sequential,
            VirtualUsers = 1,
            RequestCount = 2,
            DelayMs = 0
        };

        // Act
        var results = await _loadTestService.RunLoadTestAsync(config);

        // Assert
        Assert.NotNull(results);
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.True(r.ResponseTimeMs > 0));
    }

    [Fact]
    public async Task RunLoadTestAsync_ShouldReturnResults_WithRandomMode()
    {
        // Arrange
        var config = new TestConfig
        {
            Urls = new List<string> { "https://httpbin.org/get", "https://httpbin.org/status/200" },
            UrlMode = UrlMode.Random,
            VirtualUsers = 1,
            RequestCount = 5,
            DelayMs = 0
        };

        // Act
        var results = await _loadTestService.RunLoadTestAsync(config);

        // Assert
        Assert.NotNull(results);
        Assert.Equal(5, results.Count);
    }

    [Fact]
    public async Task RunLoadTestAsync_ShouldHandleMultipleUsers()
    {
        // Arrange
        var config = new TestConfig
        {
            Urls = new List<string> { "https://httpbin.org/get" },
            UrlMode = UrlMode.Sequential,
            VirtualUsers = 3,
            RequestCount = 2,
            DelayMs = 0
        };

        // Act
        var results = await _loadTestService.RunLoadTestAsync(config);

        // Assert
        Assert.NotNull(results);
        Assert.Equal(6, results.Count); // 3 users * 2 requests
    }

    [Fact]
    public async Task RunLoadTestAsync_ShouldHandleHttpErrors()
    {
        // Arrange
        var config = new TestConfig
        {
            Urls = new List<string> { "https://httpbin.org/status/404" },
            UrlMode = UrlMode.Sequential,
            VirtualUsers = 1,
            RequestCount = 1,
            DelayMs = 0
        };

        // Act
        var results = await _loadTestService.RunLoadTestAsync(config);

        // Assert
        Assert.NotNull(results);
        Assert.Single(results);
        Assert.False(results[0].IsSuccess);
        Assert.Equal(404, results[0].StatusCode);
    }

    [Fact]
    public async Task RunLoadTestAsync_ShouldHandleExceptions()
    {
        // Arrange
        var config = new TestConfig
        {
            Urls = new List<string> { "https://invalid-url-that-does-not-exist-12345.com" },
            UrlMode = UrlMode.Sequential,
            VirtualUsers = 1,
            RequestCount = 1,
            DelayMs = 0
        };

        // Act
        var results = await _loadTestService.RunLoadTestAsync(config);

        // Assert
        Assert.NotNull(results);
        Assert.Single(results);
        Assert.False(results[0].IsSuccess);
        Assert.NotNull(results[0].ErrorMessage);
    }

    [Fact]
    public async Task RunLoadTestAsync_ShouldCallOnResultReceived()
    {
        // Arrange
        var config = new TestConfig
        {
            Urls = new List<string> { "https://httpbin.org/get" },
            UrlMode = UrlMode.Sequential,
            VirtualUsers = 1,
            RequestCount = 2,
            DelayMs = 0
        };

        var receivedResults = new List<TestResult>();
        Action<TestResult> onResultReceived = result => receivedResults.Add(result);

        // Act
        await _loadTestService.RunLoadTestAsync(config, null, onResultReceived);

        // Assert
        Assert.Equal(2, receivedResults.Count);
    }

    [Fact]
    public async Task RunLoadTestAsync_ShouldReportProgress()
    {
        // Arrange
        var config = new TestConfig
        {
            Urls = new List<string> { "https://httpbin.org/get" },
            UrlMode = UrlMode.Sequential,
            VirtualUsers = 1,
            RequestCount = 2,
            DelayMs = 0
        };

        var progressValues = new List<double>();
        IProgress<double> progress = new Progress<double>(value => progressValues.Add(value));

        // Act
        await _loadTestService.RunLoadTestAsync(config, progress);

        // Assert
        Assert.NotEmpty(progressValues);
        Assert.True(progressValues.Last() >= 1.0); // Should reach 100%
    }

    [Fact]
    public async Task RunLoadTestAsync_ShouldRespectCancellation()
    {
        // Arrange
        var config = new TestConfig
        {
            Urls = new List<string> { "https://httpbin.org/get" },
            UrlMode = UrlMode.Sequential,
            VirtualUsers = 1,
            RequestCount = 10,
            DelayMs = 100
        };

        var cts = new CancellationTokenSource();
        cts.CancelAfter(200); // Cancel after 200ms

        // Act & Assert
        var results = await Assert.ThrowsAsync<TaskCanceledException>(
            async () => await _loadTestService.RunLoadTestAsync(config, null, null, cts.Token));
        
        Assert.NotNull(results);
    }

    [Fact]
    public async Task RunLoadTestAsync_ShouldIncludeHeaders()
    {
        // Arrange
        var config = new TestConfig
        {
            Urls = new List<string> { "https://httpbin.org/headers" },
            UrlMode = UrlMode.Sequential,
            VirtualUsers = 1,
            RequestCount = 1,
            DelayMs = 0,
            Headers = new Dictionary<string, string>
            {
                { "X-Test-Header", "test-value" }
            }
        };

        // Act
        var results = await _loadTestService.RunLoadTestAsync(config);

        // Assert
        Assert.NotNull(results);
        Assert.Single(results);
        Assert.True(results[0].IsSuccess);
    }

    public void Dispose()
    {
        _loadTestService?.Dispose();
    }
}
