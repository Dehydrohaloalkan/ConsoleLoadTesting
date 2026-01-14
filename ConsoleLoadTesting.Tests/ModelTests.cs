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

public class ScenarioConfigTests
{
    [Fact]
    public void ScenarioConfig_Parse_ShouldParseValidScenarioString()
    {
        // Act
        var scenario = ScenarioConfig.Parse("2:30:5");

        // Assert
        Assert.Equal(2, scenario.VirtualUsers);
        Assert.Equal(30, scenario.RequestCount);
        Assert.Equal(5, scenario.DurationSeconds);
    }

    [Fact]
    public void ScenarioConfig_Parse_ShouldThrowException_ForInvalidFormat()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => ScenarioConfig.Parse("2:30"));
        Assert.Throws<ArgumentException>(() => ScenarioConfig.Parse("2:30:5:extra"));
        Assert.Throws<ArgumentException>(() => ScenarioConfig.Parse("invalid"));
    }

    [Fact]
    public void ScenarioConfig_Parse_ShouldThrowException_ForInvalidNumbers()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => ScenarioConfig.Parse("0:30:5"));
        Assert.Throws<ArgumentException>(() => ScenarioConfig.Parse("2:0:5"));
        Assert.Throws<ArgumentException>(() => ScenarioConfig.Parse("2:30:0"));
        Assert.Throws<ArgumentException>(() => ScenarioConfig.Parse("-1:30:5"));
        Assert.Throws<ArgumentException>(() => ScenarioConfig.Parse("2:-5:5"));
        Assert.Throws<ArgumentException>(() => ScenarioConfig.Parse("2:30:-1"));
    }

    [Fact]
    public void ScenarioConfig_ParseScenarios_ShouldParseMultipleScenarios()
    {
        // Act
        var scenarios = ScenarioConfig.ParseScenarios("1:20:3,2:30:5,3:40:7");

        // Assert
        Assert.Equal(3, scenarios.Count);
        Assert.Equal(1, scenarios[0].VirtualUsers);
        Assert.Equal(20, scenarios[0].RequestCount);
        Assert.Equal(3, scenarios[0].DurationSeconds);
        Assert.Equal(2, scenarios[1].VirtualUsers);
        Assert.Equal(30, scenarios[1].RequestCount);
        Assert.Equal(5, scenarios[1].DurationSeconds);
        Assert.Equal(3, scenarios[2].VirtualUsers);
        Assert.Equal(40, scenarios[2].RequestCount);
        Assert.Equal(7, scenarios[2].DurationSeconds);
    }

    [Fact]
    public void ScenarioConfig_ParseScenarios_ShouldHandleEmptyAndWhitespaceStrings()
    {
        // Act
        var scenarios = ScenarioConfig.ParseScenarios("");
        var scenarios2 = ScenarioConfig.ParseScenarios("   ");
        var scenarios3 = ScenarioConfig.ParseScenarios(null);

        // Assert
        Assert.Empty(scenarios);
        Assert.Empty(scenarios2);
        Assert.Empty(scenarios3);
    }

    [Fact]
    public void ScenarioConfig_ParseScenarios_ShouldHandleScenariosWithSpaces()
    {
        // Act
        var scenarios = ScenarioConfig.ParseScenarios(" 1:20:3 , 2:30:5 ");

        // Assert
        Assert.Equal(2, scenarios.Count);
        Assert.Equal(1, scenarios[0].VirtualUsers);
        Assert.Equal(2, scenarios[1].VirtualUsers);
    }

    [Fact]
    public void ScenarioConfig_ParseScenarios_ShouldThrowException_ForInvalidScenarioInList()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => ScenarioConfig.ParseScenarios("1:20:3,invalid:scenario"));
    }
}