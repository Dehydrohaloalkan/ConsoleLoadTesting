using ConsoleLoadTesting.Models;
using Xunit;

namespace ConsoleLoadTesting.Tests;

public class TestResultTests
{
    [Fact]
    public void TestResult_ShouldInitialize_WithDefaultValues()
    {
        // Act
        var result = new TestResult();

        // Assert
        Assert.Equal(0, result.UserId);
        Assert.Equal(string.Empty, result.Url);
        Assert.Equal(0, result.StatusCode);
        Assert.Equal(0, result.ResponseTimeMs);
        Assert.False(result.IsSuccess);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(default(DateTime), result.Timestamp);
    }

    [Fact]
    public void TestResult_ShouldAllow_SettingAllProperties()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var result = new TestResult
        {
            UserId = 5,
            Url = "https://example.com",
            StatusCode = 200,
            ResponseTimeMs = 150,
            IsSuccess = true,
            ErrorMessage = null,
            Timestamp = timestamp
        };

        // Assert
        Assert.Equal(5, result.UserId);
        Assert.Equal("https://example.com", result.Url);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(150, result.ResponseTimeMs);
        Assert.True(result.IsSuccess);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(timestamp, result.Timestamp);
    }

    [Fact]
    public void TestResult_ShouldAllow_SettingErrorMessage()
    {
        // Arrange
        var result = new TestResult
        {
            ErrorMessage = "Connection timeout"
        };

        // Assert
        Assert.Equal("Connection timeout", result.ErrorMessage);
    }
}

public class TestConfigTests
{
    [Fact]
    public void TestConfig_ShouldInitialize_WithDefaultValues()
    {
        // Act
        var config = new TestConfig();

        // Assert
        Assert.NotNull(config.Urls);
        Assert.Empty(config.Urls);
        Assert.Equal(UrlMode.Sequential, config.UrlMode);
        Assert.Equal(1, config.VirtualUsers);
        Assert.Equal(1, config.RequestCount);
        Assert.Equal(0, config.DelayMs);
        Assert.NotNull(config.Headers);
        Assert.Empty(config.Headers);
        Assert.Equal(1, config.ChartTimeStepSeconds);
    }

    [Fact]
    public void TestConfig_ShouldAllow_SettingAllProperties()
    {
        // Arrange
        var config = new TestConfig
        {
            Urls = new List<string> { "https://example.com", "https://test.com" },
            UrlMode = UrlMode.Random,
            VirtualUsers = 5,
            RequestCount = 10,
            DelayMs = 100,
            Headers = new Dictionary<string, string>
            {
                { "Authorization", "Bearer token" }
            },
            ChartTimeStepSeconds = 2
        };

        // Assert
        Assert.Equal(2, config.Urls.Count);
        Assert.Equal(UrlMode.Random, config.UrlMode);
        Assert.Equal(5, config.VirtualUsers);
        Assert.Equal(10, config.RequestCount);
        Assert.Equal(100, config.DelayMs);
        Assert.Single(config.Headers);
        Assert.Equal(2, config.ChartTimeStepSeconds);
    }

    [Fact]
    public void TestConfig_ShouldAllow_AddingUrls()
    {
        // Arrange
        var config = new TestConfig();

        // Act
        config.Urls.Add("https://example.com");
        config.Urls.Add("https://test.com");

        // Assert
        Assert.Equal(2, config.Urls.Count);
    }

    [Fact]
    public void TestConfig_ShouldAllow_AddingHeaders()
    {
        // Arrange
        var config = new TestConfig();

        // Act
        config.Headers.Add("User-Agent", "TestAgent");
        config.Headers.Add("Authorization", "Bearer token123");

        // Assert
        Assert.Equal(2, config.Headers.Count);
        Assert.Equal("TestAgent", config.Headers["User-Agent"]);
        Assert.Equal("Bearer token123", config.Headers["Authorization"]);
    }
}
