using System.Text.Json;
using System.Text.Json.Serialization;
using ConsoleLoadTesting.Models;
using Spectre.Console;

namespace ConsoleLoadTesting.Services;

// Кастомный конвертер для enum, который не чувствителен к регистру
public class CaseInsensitiveEnumConverter<T> : JsonConverter<T> where T : struct, Enum
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (string.IsNullOrEmpty(value))
            return default;

        if (Enum.TryParse<T>(value, ignoreCase: true, out var result))
            return result;

        throw new JsonException($"Неизвестное значение enum: {value}");
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

public class ConfigService
{
    public TestConfig? LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            AnsiConsole.MarkupLine($"[red]Ошибка: Файл конфигурации не найден: {filePath}[/]");
            return null;
        }

        try
        {
            var json = File.ReadAllText(filePath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new CaseInsensitiveEnumConverter<UrlMode>() }
            };
            
            var config = JsonSerializer.Deserialize<TestConfig>(json, options);
            
            if (config == null)
            {
                AnsiConsole.MarkupLine("[red]Ошибка: Не удалось десериализовать конфигурацию из файла[/]");
                return null;
            }

            // Валидация конфигурации
            if (config.Urls == null || config.Urls.Count == 0)
            {
                AnsiConsole.MarkupLine("[red]Ошибка: В конфигурации не указаны URL-адреса[/]");
                return null;
            }

            return config;
        }
        catch (JsonException ex)
        {
            AnsiConsole.MarkupLine($"[red]Ошибка парсинга JSON: {ex.Message}[/]");
            return null;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Ошибка при загрузке конфигурации: {ex.Message}[/]");
            return null;
        }
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
        
        // Создаём директорию, если её нет
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        File.WriteAllText(filePath, json);
    }
}
