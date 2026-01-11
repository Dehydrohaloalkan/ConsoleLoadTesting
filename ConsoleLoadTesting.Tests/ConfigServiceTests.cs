using System.Text.Json;
using ConsoleLoadTesting.Models;
using ConsoleLoadTesting.Services;
using Xunit;

namespace ConsoleLoadTesting.Tests;

public class ConfigServiceTests : IDisposable
{
    private readonly ConfigService _configService;
    private readonly string _testDir;
    private readonly string _testFilePath;

    public ConfigServiceTests()
    {
        _configService = new ConfigService();
        _testDir = Path.Combine(Path.GetTempPath(), "ConsoleLoadTestingTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDir);
        _testFilePath = Path.Combine(_testDir, "test_config.json");
    }

    [Fact]
    public void LoadFromFile_ShouldLoadValidConfig()
    {
        // Arrange
        var config = new TestConfig
        {
            Urls = new List<string> { "https://example.com", "https://test.com" },
            UrlMode = UrlMode.Sequential,
            VirtualUsers = 5,
            RequestCount = 10,
            DelayMs = 100,
            Headers = new Dictionary<string, string>
            {
                { "Authorization", "Bearer token123" }
            }
        };

        var json = """
        {
            "Urls": ["https://example.com", "https://test.com"],
            "UrlMode": "Sequential",
            "VirtualUsers": 5,
            "RequestCount": 10,
            "DelayMs": 100,
            "Headers": {
                "Authorization": "Bearer token123"
            }
        }
        """;
        File.WriteAllText(_testFilePath, json);

        // Act
        var loadedConfig = _configService.LoadFromFile(_testFilePath);

        // Assert
        Assert.NotNull(loadedConfig);
        Assert.Equal(2, loadedConfig!.Urls.Count);
        Assert.Equal(UrlMode.Sequential, loadedConfig.UrlMode);
        Assert.Equal(5, loadedConfig.VirtualUsers);
        Assert.Equal(10, loadedConfig.RequestCount);
        Assert.Equal(100, loadedConfig.DelayMs);
        Assert.Single(loadedConfig.Headers);
        Assert.Equal("Bearer token123", loadedConfig.Headers["Authorization"]);
    }

    [Fact]
    public void LoadFromFile_ShouldHandleCaseInsensitiveEnum()
    {
        // Arrange
        var json = """
        {
            "urls": ["https://example.com"],
            "urlMode": "random",
            "virtualUsers": 1,
            "requestCount": 1
        }
        """;
        File.WriteAllText(_testFilePath, json);

        // Act
        var loadedConfig = _configService.LoadFromFile(_testFilePath);

        // Assert
        Assert.NotNull(loadedConfig);
        Assert.Equal(UrlMode.Random, loadedConfig!.UrlMode);
    }

    [Fact]
    public void LoadFromFile_ShouldReturnNull_WhenFileNotFound()
    {
        // Act
        var result = _configService.LoadFromFile("nonexistent.json");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void LoadFromFile_ShouldReturnNull_WhenInvalidJson()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "invalid json {");

        // Act
        var result = _configService.LoadFromFile(_testFilePath);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void LoadFromFile_ShouldReturnNull_WhenNoUrls()
    {
        // Arrange
        var json = """
        {
            "urls": [],
            "virtualUsers": 1,
            "requestCount": 1
        }
        """;
        File.WriteAllText(_testFilePath, json);

        // Act
        var result = _configService.LoadFromFile(_testFilePath);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void LoadFromFile_ShouldReturnNull_WhenUrlsIsNull()
    {
        // Arrange
        var json = """
        {
            "urls": null,
            "virtualUsers": 1,
            "requestCount": 1
        }
        """;
        File.WriteAllText(_testFilePath, json);

        // Act
        var result = _configService.LoadFromFile(_testFilePath);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void SaveToFile_ShouldCreateFile_WithCorrectContent()
    {
        // Arrange
        var config = new TestConfig
        {
            Urls = new List<string> { "https://example.com" },
            UrlMode = UrlMode.Sequential,
            VirtualUsers = 3,
            RequestCount = 5,
            DelayMs = 50,
            Headers = new Dictionary<string, string>
            {
                { "User-Agent", "TestAgent" }
            }
        };

        // Act
        _configService.SaveToFile(config, _testFilePath);

        // Assert
        Assert.True(File.Exists(_testFilePath));
        var json = File.ReadAllText(_testFilePath);
        Assert.NotEmpty(json);
        
        var loadedConfig = _configService.LoadFromFile(_testFilePath);
        Assert.NotNull(loadedConfig);
        Assert.Equal(config.Urls.Count, loadedConfig!.Urls.Count);
        Assert.Equal(config.UrlMode, loadedConfig.UrlMode);
        Assert.Equal(config.VirtualUsers, loadedConfig.VirtualUsers);
    }

    [Fact]
    public void SaveToFile_ShouldCreateDirectory_IfNotExists()
    {
        // Arrange
        var subDir = Path.Combine(_testDir, "subdir");
        var filePath = Path.Combine(subDir, "config.json");
        var config = new TestConfig
        {
            Urls = new List<string> { "https://example.com" }
        };

        // Act
        _configService.SaveToFile(config, filePath);

        // Assert
        Assert.True(Directory.Exists(subDir));
        Assert.True(File.Exists(filePath));
    }

    [Fact]
    public void SaveToFile_And_LoadFromFile_ShouldRoundTrip()
    {
        // Arrange
        var originalConfig = new TestConfig
        {
            Urls = new List<string> { "https://example.com", "https://test.com" },
            UrlMode = UrlMode.Random,
            VirtualUsers = 7,
            RequestCount = 15,
            DelayMs = 200,
            Headers = new Dictionary<string, string>
            {
                { "X-Custom-Header", "custom-value" },
                { "Authorization", "Bearer token" }
            },
            ChartTimeStepSeconds = 2
        };

        // Act
        _configService.SaveToFile(originalConfig, _testFilePath);
        var loadedConfig = _configService.LoadFromFile(_testFilePath);

        // Assert
        Assert.NotNull(loadedConfig);
        Assert.Equal(originalConfig.Urls.Count, loadedConfig!.Urls.Count);
        Assert.Equal(originalConfig.Urls[0], loadedConfig.Urls[0]);
        Assert.Equal(originalConfig.Urls[1], loadedConfig.Urls[1]);
        Assert.Equal(originalConfig.UrlMode, loadedConfig.UrlMode);
        Assert.Equal(originalConfig.VirtualUsers, loadedConfig.VirtualUsers);
        Assert.Equal(originalConfig.RequestCount, loadedConfig.RequestCount);
        Assert.Equal(originalConfig.DelayMs, loadedConfig.DelayMs);
        Assert.Equal(originalConfig.Headers.Count, loadedConfig.Headers.Count);
        Assert.Equal(originalConfig.ChartTimeStepSeconds, loadedConfig.ChartTimeStepSeconds);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, true);
        }
    }
}
