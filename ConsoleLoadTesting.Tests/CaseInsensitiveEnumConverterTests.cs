using System.Text.Json;
using ConsoleLoadTesting.Models;
using ConsoleLoadTesting.Services;
using Xunit;

namespace ConsoleLoadTesting.Tests;

public class CaseInsensitiveEnumConverterTests
{
    [Theory]
    [InlineData("Sequential", UrlMode.Sequential)]
    [InlineData("sequential", UrlMode.Sequential)]
    [InlineData("SEQUENTIAL", UrlMode.Sequential)]
    [InlineData("Random", UrlMode.Random)]
    [InlineData("random", UrlMode.Random)]
    [InlineData("RANDOM", UrlMode.Random)]
    public void Read_ShouldParseEnum_CaseInsensitive(string value, UrlMode expected)
    {
        // Arrange
        var converter = new CaseInsensitiveEnumConverter<UrlMode>();
        var json = $"\"{value}\"";
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(bytes);

        // Act
        reader.Read(); // Move to value
        var result = converter.Read(ref reader, typeof(UrlMode), new JsonSerializerOptions());

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Read_ShouldReturnDefault_WhenValueIsEmpty()
    {
        // Arrange
        var converter = new CaseInsensitiveEnumConverter<UrlMode>();
        var json = "\"\"";
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(bytes);

        // Act
        reader.Read(); // Move to value
        var result = converter.Read(ref reader, typeof(UrlMode), new JsonSerializerOptions());

        // Assert
        Assert.Equal(default(UrlMode), result);
    }

    [Fact]
    public void Read_ShouldThrow_WhenInvalidValue()
    {
        // Arrange
        var converter = new CaseInsensitiveEnumConverter<UrlMode>();
        var json = "\"InvalidMode\"";
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(bytes);
        reader.Read(); // Move to value

        // Act & Assert
        Exception? exception = null;
        try
        {
            converter.Read(ref reader, typeof(UrlMode), new JsonSerializerOptions());
        }
        catch (JsonException ex)
        {
            exception = ex;
        }

        Assert.NotNull(exception);
        Assert.IsType<JsonException>(exception);
    }

    [Fact]
    public void Write_ShouldWriteEnum_AsString()
    {
        // Arrange
        var converter = new CaseInsensitiveEnumConverter<UrlMode>();
        var memoryStream = new MemoryStream();
        var writer = new Utf8JsonWriter(memoryStream);

        // Act
        converter.Write(writer, UrlMode.Sequential, new JsonSerializerOptions());
        writer.Flush();

        // Assert
        memoryStream.Position = 0;
        var json = System.Text.Encoding.UTF8.GetString(memoryStream.ToArray());
        Assert.Equal("\"Sequential\"", json);
    }
}
