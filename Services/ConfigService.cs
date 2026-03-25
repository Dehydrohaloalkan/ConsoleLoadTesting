using System.Text.Json;
using System.Text.Json.Serialization;
using ConsoleLoadTesting.Models;

namespace ConsoleLoadTesting.Services;

// Case-insensitive enum converter
public class CaseInsensitiveEnumConverter<T> : JsonConverter<T> where T : struct, Enum
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (string.IsNullOrEmpty(value))
            return default;

        if (Enum.TryParse<T>(value, ignoreCase: true, out var result))
            return result;

        throw new JsonException($"Unknown enum value: {value}");
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

public class ConfigService
{
    public TestConfig LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Config file not found: {filePath}", filePath);
        }

        var json = File.ReadAllText(filePath);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new CaseInsensitiveEnumConverter<UrlMode>() }
        };

        var config = JsonSerializer.Deserialize<TestConfig>(json, options)
            ?? throw new InvalidDataException("Failed to deserialize configuration");

        if (config.Urls == null || config.Urls.Count == 0)
        {
            throw new InvalidDataException("No URLs provided in configuration");
        }

        return config;
    }

    public void SaveToFile(TestConfig config, string filePath)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            Converters = { new CaseInsensitiveEnumConverter<UrlMode>() }
        };
        var json = JsonSerializer.Serialize(config, options);
        
        // Create directory if needed
        FileService.EnsureOutputDirectory(filePath);

        File.WriteAllText(filePath, json);
    }
}
