using System.Globalization;
using ConsoleLoadTesting.Models;
using ConsoleLoadTesting.Services;
using Xunit;

namespace ConsoleLoadTesting.Tests;

public class ResultServiceTests : IDisposable
{
    private readonly ResultService _resultService;
    private readonly string _testDir;
    private readonly string _testFilePath;

    public ResultServiceTests()
    {
        _resultService = new ResultService();
        _testDir = Path.Combine(Path.GetTempPath(), "ConsoleLoadTestingTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDir);
        _testFilePath = Path.Combine(_testDir, "test_results.csv");
    }

    [Fact]
    public void SaveResultsToFile_ShouldCreateFile_WithCorrectContent()
    {
        // Arrange
        var results = new List<TestResult>
        {
            new TestResult
            {
                UserId = 1,
                Url = "https://example.com",
                StatusCode = 200,
                ResponseTimeMs = 150,
                Timestamp = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc),
                IsSuccess = true,
                ErrorMessage = null
            },
            new TestResult
            {
                UserId = 2,
                Url = "https://example.com/page",
                StatusCode = 404,
                ResponseTimeMs = 50,
                Timestamp = new DateTime(2024, 1, 1, 12, 0, 1, DateTimeKind.Utc),
                IsSuccess = false,
                ErrorMessage = "Not Found"
            }
        };

        // Act
        _resultService.SaveResultsToFile(results, _testFilePath);

        // Assert
        Assert.True(File.Exists(_testFilePath));
        var lines = File.ReadAllLines(_testFilePath);
        Assert.Equal(3, lines.Length); // Header + 2 results
        Assert.Equal("UserId,Url,StatusCode,TimeMs,Timestamp,IsSuccess,ErrorMessage", lines[0]);
    }

    [Fact]
    public void SaveResultsToFile_ShouldEscapeCsv_WhenUrlContainsComma()
    {
        // Arrange
        var results = new List<TestResult>
        {
            new TestResult
            {
                UserId = 1,
                Url = "https://example.com?param=1,2",
                StatusCode = 200,
                ResponseTimeMs = 100,
                Timestamp = DateTime.UtcNow,
                IsSuccess = true
            }
        };

        // Act
        _resultService.SaveResultsToFile(results, _testFilePath);

        // Assert
        var lines = File.ReadAllLines(_testFilePath);
        Assert.Contains("\"https://example.com?param=1,2\"", lines[1]);
    }

    [Fact]
    public void LoadResultsFromFile_ShouldLoadCorrectResults()
    {
        // Arrange
        var expectedResults = new List<TestResult>
        {
            new TestResult
            {
                UserId = 1,
                Url = "https://example.com",
                StatusCode = 200,
                ResponseTimeMs = 150,
                Timestamp = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc),
                IsSuccess = true,
                ErrorMessage = null
            }
        };
        
        var csvContent = "UserId,Url,StatusCode,TimeMs,Timestamp,IsSuccess,ErrorMessage\n" +
                        "1,https://example.com,200,150,2024-01-01T12:00:00.0000000Z,True,";
        File.WriteAllText(_testFilePath, csvContent);

        // Act
        var loadedResults = _resultService.LoadResultsFromFile(_testFilePath);

        // Assert
        Assert.NotNull(loadedResults);
        Assert.Single(loadedResults);
        Assert.Equal(1, loadedResults![0].UserId);
        Assert.Equal("https://example.com", loadedResults[0].Url);
        Assert.Equal(200, loadedResults[0].StatusCode);
        Assert.Equal(150, loadedResults[0].ResponseTimeMs);
        Assert.True(loadedResults[0].IsSuccess);
    }

    [Fact]
    public void LoadResultsFromFile_ShouldHandleQuotedFields()
    {
        // Arrange
        var csvContent = "UserId,Url,StatusCode,TimeMs,Timestamp,IsSuccess,ErrorMessage\n" +
                        "1,\"https://example.com?param=1,2\",200,150,2024-01-01T12:00:00.0000000Z,True,";
        File.WriteAllText(_testFilePath, csvContent);

        // Act
        var loadedResults = _resultService.LoadResultsFromFile(_testFilePath);

        // Assert
        Assert.NotNull(loadedResults);
        Assert.Single(loadedResults);
        Assert.Equal("https://example.com?param=1,2", loadedResults![0].Url);
    }

    [Fact]
    public void LoadResultsFromFile_ShouldReturnNull_WhenFileNotFound()
    {
        // Act
        var result = _resultService.LoadResultsFromFile("nonexistent.csv");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void LoadResultsFromFile_ShouldReturnNull_WhenFileEmpty()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "");

        // Act
        var result = _resultService.LoadResultsFromFile(_testFilePath);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void CreateUrlStatsTable_ShouldCreateTable_WithCorrectData()
    {
        // Arrange
        var results = new List<TestResult>
        {
            new TestResult { UserId = 1, Url = "https://example.com", StatusCode = 200, ResponseTimeMs = 100, Timestamp = DateTime.UtcNow, IsSuccess = true },
            new TestResult { UserId = 1, Url = "https://example.com", StatusCode = 200, ResponseTimeMs = 150, Timestamp = DateTime.UtcNow, IsSuccess = true },
            new TestResult { UserId = 2, Url = "https://test.com", StatusCode = 200, ResponseTimeMs = 200, Timestamp = DateTime.UtcNow, IsSuccess = true }
        };
        var urls = new List<string> { "https://example.com", "https://test.com" };
        var startTime = DateTime.UtcNow.AddSeconds(-1);

        // Act
        var table = _resultService.CreateUrlStatsTable(results, urls, startTime);

        // Assert
        Assert.NotNull(table);
    }

    [Fact]
    public void DisplayResults_ShouldNotThrow_WithEmptyList()
    {
        // Arrange
        var results = new List<TestResult>();

        // Act & Assert
        _resultService.DisplayResults(results);
    }

    [Fact]
    public void DisplayResults_ShouldNotThrow_WithValidResults()
    {
        // Arrange
        var results = new List<TestResult>
        {
            new TestResult { UserId = 1, Url = "https://example.com", StatusCode = 200, ResponseTimeMs = 100, Timestamp = DateTime.UtcNow, IsSuccess = true },
            new TestResult { UserId = 1, Url = "https://example.com", StatusCode = 500, ResponseTimeMs = 200, Timestamp = DateTime.UtcNow, IsSuccess = false }
        };

        // Act & Assert
        _resultService.DisplayResults(results);
    }

    [Fact]
    public void DisplaySummaryReport_ShouldNotThrow_WithValidData()
    {
        // Arrange
        var testResultsDict = new Dictionary<string, List<TestResult>>
        {
            ["test1.csv"] = new List<TestResult>
            {
                new TestResult { UserId = 1, Url = "https://example.com", StatusCode = 200, ResponseTimeMs = 100, Timestamp = DateTime.UtcNow, IsSuccess = true }
            }
        };

        // Act & Assert
        _resultService.DisplaySummaryReport(testResultsDict);
    }

    [Fact]
    public void DisplaySummaryReport_ShouldHandleEmptyDictionary()
    {
        // Arrange
        var testResultsDict = new Dictionary<string, List<TestResult>>();

        // Act & Assert
        _resultService.DisplaySummaryReport(testResultsDict);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, true);
        }
    }
}
