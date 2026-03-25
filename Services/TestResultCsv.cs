using System.Globalization;
using ConsoleLoadTesting.Models;

namespace ConsoleLoadTesting.Services;

internal static class TestResultCsv
{
    public const string Header = "UserId,Url,StatusCode,TimeMs,Timestamp,IsSuccess,ErrorMessage";

    public static void EnsureOutputDirectory(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public static string Serialize(TestResult result)
    {
        var timestamp = result.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture);
        return string.Join(',',
            result.UserId.ToString(CultureInfo.InvariantCulture),
            Escape(result.Url),
            result.StatusCode.ToString(CultureInfo.InvariantCulture),
            result.ResponseTimeMs.ToString(CultureInfo.InvariantCulture),
            timestamp,
            result.IsSuccess.ToString(),
            Escape(result.ErrorMessage ?? string.Empty));
    }

    public static bool TryParse(string line, out TestResult? result)
    {
        result = null;

        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var parts = ParseLine(line);
        if (parts.Count < 6)
        {
            return false;
        }

        if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var userId) ||
            !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var statusCode) ||
            !long.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var responseTimeMs) ||
            !DateTime.TryParse(parts[4], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var timestamp) ||
            !bool.TryParse(parts[5], out var isSuccess))
        {
            return false;
        }

        result = new TestResult
        {
            UserId = userId,
            Url = parts[1],
            StatusCode = statusCode,
            ResponseTimeMs = responseTimeMs,
            Timestamp = timestamp,
            IsSuccess = isSuccess,
            ErrorMessage = parts.Count > 6 && !string.IsNullOrEmpty(parts[6]) ? parts[6] : null
        };

        return true;
    }

    private static string Escape(string value)
    {
        var normalized = value
            .Replace("\r\n", " ")
            .Replace('\r', ' ')
            .Replace('\n', ' ');

        if (normalized.Contains(',') || normalized.Contains('"'))
        {
            return $"\"{normalized.Replace("\"", "\"\"")}\"";
        }

        return normalized;
    }

    private static List<string> ParseLine(string line)
    {
        var parts = new List<string>();
        var currentPart = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var currentChar = line[i];

            if (currentChar == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    currentPart.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (currentChar == ',' && !inQuotes)
            {
                parts.Add(currentPart.ToString());
                currentPart.Clear();
            }
            else
            {
                currentPart.Append(currentChar);
            }
        }

        parts.Add(currentPart.ToString());
        return parts;
    }
}
