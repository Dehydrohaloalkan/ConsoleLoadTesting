namespace ConsoleLoadTesting.Models;

public class ScenarioConfig
{
    public int VirtualUsers { get; set; }
    public int RequestCount { get; set; }
    public int DurationSeconds { get; set; }

    public static ScenarioConfig Parse(string scenarioString)
    {
        var parts = scenarioString.Split(':');
        if (parts.Length != 3)
        {
            throw new ArgumentException($"Неверный формат сценария: {scenarioString}. Ожидается формат: users:requests:duration");
        }

        if (!int.TryParse(parts[0], out var users) || users < 1)
        {
            throw new ArgumentException($"Неверное количество пользователей в сценарии: {parts[0]}");
        }

        if (!int.TryParse(parts[1], out var requests) || requests < 1)
        {
            throw new ArgumentException($"Неверное количество запросов в сценарии: {parts[1]}");
        }

        if (!int.TryParse(parts[2], out var duration) || duration < 1)
        {
            throw new ArgumentException($"Неверная длительность в сценарии: {parts[2]}");
        }

        return new ScenarioConfig
        {
            VirtualUsers = users,
            RequestCount = requests,
            DurationSeconds = duration
        };
    }

    public static List<ScenarioConfig> ParseScenarios(string scenariosString)
    {
        if (string.IsNullOrWhiteSpace(scenariosString))
        {
            return new List<ScenarioConfig>();
        }

        return scenariosString.Split(',')
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .Select(Parse)
            .ToList();
    }
}