using System.CommandLine;
using Spectre.Console;
using ConsoleLoadTesting.Models;
using ConsoleLoadTesting.Services;

var rootCommand = new RootCommand("Приложение для нагрузочного тестирования сайтов");

// Опции командной строки
var urlsOption = new Option<string[]>(
    aliases: new[] { "--urls", "-u" },
    description: "URL-адреса для тестирования (можно указать несколько)"
)
{
    AllowMultipleArgumentsPerToken = true
};

var urlModeOption = new Option<string>(
    aliases: new[] { "--mode", "-m" },
    description: "Режим работы с ссылками: sequential или random",
    getDefaultValue: () => "sequential"
);

var usersOption = new Option<int>(
    aliases: new[] { "--users", "-v" },
    description: "Количество виртуальных пользователей",
    getDefaultValue: () => 1
);

var requestsOption = new Option<int>(
    aliases: new[] { "--requests", "-r" },
    description: "Количество запросов на пользователя",
    getDefaultValue: () => 1
);

var delayOption = new Option<int>(
    aliases: new[] { "--delay", "-d" },
    description: "Задержка между запросами в миллисекундах",
    getDefaultValue: () => 0
);

var headersOption = new Option<string[]>(
    aliases: new[] { "--header", "-H" },
    description: "Заголовки запроса в формате 'Name:Value' (можно указать несколько)"
)
{
    AllowMultipleArgumentsPerToken = true
};

var configOption = new Option<string?>(
    aliases: new[] { "--config", "-c" },
    description: "Путь к файлу конфигурации (JSON)"
);

var saveOption = new Option<string?>(
    aliases: new[] { "--save", "-s" },
    description: "Путь для сохранения результатов в CSV файл"
);

var loadOption = new Option<string[]>(
    aliases: new[] { "--load", "-l" },
    description: "Пути к файлам результатов для анализа (можно указать несколько)"
)
{
    AllowMultipleArgumentsPerToken = true
};

var scenariosOption = new Option<string>(
    aliases: new[] { "--scenarios", "--scenario" },
    description: "Сценарии тестирования в формате 'users:requests:duration,users:requests:duration' (например: '1:20:3,2:30:5')"
);

rootCommand.AddOption(urlsOption);
rootCommand.AddOption(urlModeOption);
rootCommand.AddOption(usersOption);
rootCommand.AddOption(requestsOption);
rootCommand.AddOption(delayOption);
rootCommand.AddOption(headersOption);
rootCommand.AddOption(configOption);
rootCommand.AddOption(saveOption);
rootCommand.AddOption(loadOption);
rootCommand.AddOption(scenariosOption);

rootCommand.SetHandler(async (context) =>
{
    var urls = context.ParseResult.GetValueForOption(urlsOption) ?? Array.Empty<string>();
    var urlMode = context.ParseResult.GetValueForOption(urlModeOption) ?? "sequential";
    var users = context.ParseResult.GetValueForOption(usersOption);
    var requests = context.ParseResult.GetValueForOption(requestsOption);
    var delay = context.ParseResult.GetValueForOption(delayOption);
    var headers = context.ParseResult.GetValueForOption(headersOption) ?? Array.Empty<string>();
    var configPath = context.ParseResult.GetValueForOption(configOption);
    var savePath = context.ParseResult.GetValueForOption(saveOption);
    var loadPaths = context.ParseResult.GetValueForOption(loadOption) ?? Array.Empty<string>();
    var scenariosString = context.ParseResult.GetValueForOption(scenariosOption);

    var resultService = new ResultService();

    // Режим загрузки и анализа файлов
    if (loadPaths.Length > 0)
    {
        resultService.DisplaySummaryReport(loadPaths, 1);
        return;
    }

    var configService = new ConfigService();
    var interactiveService = new InteractiveService();
    var loadTestService = new LoadTestService();
    var resultServiceForTest = new ResultService();

    TestConfig? config = null;

    // Режим 1: Конфигурационный файл
    if (!string.IsNullOrEmpty(configPath))
    {
        config = configService.LoadFromFile(configPath);
        if (config == null)
        {
            AnsiConsole.MarkupLine($"[red]Ошибка: Не удалось загрузить конфигурацию из файла {configPath}[/]");
            context.ExitCode = 1;
            return;
        }
    }
    // Режим 2: Аргументы командной строки
    else if (urls.Length > 0)
    {
        config = new TestConfig
        {
            Urls = urls.ToList(),
            UrlMode = urlMode.Equals("random", StringComparison.OrdinalIgnoreCase)
                ? UrlMode.Random
                : UrlMode.Sequential,
            VirtualUsers = users,
            RequestCount = requests,
            DelayMs = delay
        };

        // Парсинг сценариев
        if (!string.IsNullOrEmpty(scenariosString))
        {
            try
            {
                config.Scenarios = ScenarioConfig.ParseScenarios(scenariosString);
                config.UseScenarios = true;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Ошибка парсинга сценариев: {ex.Message}[/]");
                context.ExitCode = 1;
                return;
            }
        }

        // Парсинг заголовков
        ParseHeaders(headers, config.Headers);
    }
    // Режим 3: Интерактивный режим
    else
    {
        config = interactiveService.GetConfigInteractively();
    }

    if (config == null || config.Urls.Count == 0)
    {
        AnsiConsole.MarkupLine("[red]Ошибка: Не указаны URL-адреса для тестирования[/]");
        context.ExitCode = 1;
        return;
    }

    if (!ValidateConfig(config))
    {
        context.ExitCode = 1;
        return;
    }

    // Запуск тестирования
    AnsiConsole.MarkupLine("[bold green]Запуск нагрузочного тестирования...[/]");
    AnsiConsole.WriteLine();

    var outputPath = ResolveOutputPath(savePath);
    var realtimeStats = new RealtimeStats();
    var statsLock = new object();
    var startTime = DateTime.UtcNow;

    AnsiConsole.MarkupLine($"[grey]Файл результатов: {outputPath}[/]");

    await using var resultWriter = new ResultWriter(outputPath);

    try
    {
        await AnsiConsole.Live(resultServiceForTest.CreateUrlStatsTable(realtimeStats, config.Urls, startTime))
            .StartAsync(async ctx =>
            {
                var onResultReceived = new Action<TestResult>(result =>
                {
                    lock (statsLock)
                    {
                        realtimeStats.Add(result);
                        ctx.UpdateTarget(resultServiceForTest.CreateUrlStatsTable(realtimeStats, config.Urls, startTime));
                    }
                });

                if (config.UseScenarios)
                {
                    await loadTestService.RunScenariosLoadTestAsync(
                        config,
                        null,
                        onResultReceived,
                        resultWriter,
                        CancellationToken.None);
                }
                else
                {
                    await loadTestService.RunLoadTestAsync(
                        config,
                        null,
                        onResultReceived,
                        resultWriter,
                        CancellationToken.None);
                }
            });
    }
    finally
    {
        await resultWriter.CompleteAsync();
        loadTestService.Dispose();
    }

    AnsiConsole.WriteLine();

    // Вывод результатов
    resultServiceForTest.DisplayResultsFromFile(outputPath, config.ChartTimeStepSeconds);
});

// Обработка команды help
var helpCommand = new Command("help", "Показать справку");
rootCommand.AddCommand(helpCommand);

helpCommand.SetHandler(() =>
{
    AnsiConsole.MarkupLine("[bold cyan]Приложение для нагрузочного тестирования сайтов[/]");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[bold]Использование:[/]");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[yellow]1. Режим аргументов командной строки:[/]");
    AnsiConsole.MarkupLine("   app.exe --urls https://example.com --users 5 --requests 10");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[yellow]2. Режим конфигурационного файла:[/]");
    AnsiConsole.MarkupLine("   app.exe --config config.json");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[yellow]3. Интерактивный режим:[/]");
    AnsiConsole.MarkupLine("   app.exe");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[bold]Параметры:[/]");
    AnsiConsole.MarkupLine("  --urls, -u          URL-адреса для тестирования (можно несколько)");
    AnsiConsole.MarkupLine("  --mode, -m          Режим работы: sequential или random");
    AnsiConsole.MarkupLine("  --users, -v         Количество виртуальных пользователей");
    AnsiConsole.MarkupLine("  --requests, -r      Количество запросов на пользователя");
    AnsiConsole.MarkupLine("  --delay, -d         Задержка между запросами (мс)");
    AnsiConsole.MarkupLine("  --scenarios         Сценарии в формате 'users:requests:duration,users:requests:duration'");
    AnsiConsole.MarkupLine("  --header, -H        Заголовки в формате 'Name:Value'");
    AnsiConsole.MarkupLine("  --config, -c        Путь к файлу конфигурации (JSON)");
    AnsiConsole.MarkupLine("  --save, -s          Путь для сохранения результатов (CSV)");
    AnsiConsole.MarkupLine("  --load, -l          Пути к файлам результатов для анализа");
    AnsiConsole.MarkupLine("  help                Показать эту справку");
});

// Helper методы
static void ParseHeaders(string[] headers, Dictionary<string, string> targetDict)
{
    foreach (var header in headers)
    {
        var parts = header.Split(':', 2);
        if (parts.Length == 2)
        {
            targetDict[parts[0].Trim()] = parts[1].Trim();
        }
    }
}

static string ResolveOutputPath(string? savePath)
{
    if (!string.IsNullOrWhiteSpace(savePath))
    {
        return Path.GetFullPath(savePath);
    }

    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
    return Path.Combine(Path.GetTempPath(), $"ConsoleLoadTesting_{timestamp}.csv");
}

static bool ValidateConfig(TestConfig config)
{
    if (config.UseScenarios)
    {
        if (config.Scenarios.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]Ошибка: Не указаны сценарии для тестирования[/]");
            return false;
        }

        foreach (var scenario in config.Scenarios)
        {
            if (scenario.VirtualUsers < 1)
            {
                AnsiConsole.MarkupLine($"[red]Ошибка: Количество виртуальных пользователей в сценарии должно быть больше 0[/]");
                return false;
            }

            if (scenario.RequestCount < 1)
            {
                AnsiConsole.MarkupLine($"[red]Ошибка: Количество запросов в сценарии должно быть больше 0[/]");
                return false;
            }

            if (scenario.DurationSeconds < 1)
            {
                AnsiConsole.MarkupLine($"[red]Ошибка: Длительность сценария должна быть больше 0 секунд[/]");
                return false;
            }
        }
    }
    else
    {
        if (config.VirtualUsers < 1)
        {
            AnsiConsole.MarkupLine("[red]Ошибка: Количество виртуальных пользователей должно быть больше 0[/]");
            return false;
        }

        if (config.RequestCount < 1)
        {
            AnsiConsole.MarkupLine("[red]Ошибка: Количество запросов должно быть больше 0[/]");
            return false;
        }
    }

    return true;
}

return await rootCommand.InvokeAsync(args);
