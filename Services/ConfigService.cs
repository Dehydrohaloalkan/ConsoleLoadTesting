using System.Text.Json;
using ConsoleLoadTesting.Models;

namespace ConsoleLoadTesting.Services;

public class ConfigService
{
    public TestConfig? LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(filePath);
            var config = JsonSerializer.Deserialize<TestConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            return config;
        }
        catch
        {
            return null;
        }
    }

    public void SaveToFile(TestConfig config, string filePath)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        var json = JsonSerializer.Serialize(config, options);
        
        // Создаём директорию, если её нет
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        File.WriteAllText(filePath, json);
    }
}
