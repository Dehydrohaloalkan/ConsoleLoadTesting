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

rootCommand.AddOption(urlsOption);
rootCommand.AddOption(urlModeOption);
rootCommand.AddOption(usersOption);
rootCommand.AddOption(requestsOption);
rootCommand.AddOption(delayOption);
rootCommand.AddOption(headersOption);
rootCommand.AddOption(configOption);
rootCommand.AddOption(saveOption);

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

    var configService = new ConfigService();
    var interactiveService = new InteractiveService();
    var loadTestService = new LoadTestService();
    var resultService = new ResultService();

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

        // Парсинг заголовков
        foreach (var header in headers)
        {
            var parts = header.Split(':', 2);
            if (parts.Length == 2)
            {
                config.Headers[parts[0].Trim()] = parts[1].Trim();
            }
        }
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

    // Валидация конфигурации
    if (config.VirtualUsers < 1)
    {
        AnsiConsole.MarkupLine("[red]Ошибка: Количество виртуальных пользователей должно быть больше 0[/]");
        context.ExitCode = 1;
        return;
    }

    if (config.RequestCount < 1)
    {
        AnsiConsole.MarkupLine("[red]Ошибка: Количество запросов должно быть больше 0[/]");
        context.ExitCode = 1;
        return;
    }

    // Запуск тестирования
    AnsiConsole.MarkupLine("[bold green]Запуск нагрузочного тестирования...[/]");
    AnsiConsole.WriteLine();

    var progress = new Progress<double>(p =>
    {
        // Прогресс будет отображаться через AnsiConsole.Progress
    });

    List<TestResult> results = new();

    await AnsiConsole.Progress()
        .Columns(new ProgressColumn[]
        {
            new TaskDescriptionColumn(),
            new ProgressBarColumn(),
            new PercentageColumn(),
            new SpinnerColumn()
        })
        .StartAsync(async ctx =>
        {
            var task = ctx.AddTask("[green]Выполнение запросов[/]", maxValue: config.VirtualUsers * config.RequestCount);
            
            var progress = new Progress<double>(p =>
            {
                task.Value = (int)(p * task.MaxValue);
            });

            results = await loadTestService.RunLoadTestAsync(config, progress, CancellationToken.None);
            task.Value = task.MaxValue;
        });

    AnsiConsole.WriteLine();

    // Вывод результатов
    resultService.DisplayResults(results);

    // Сохранение результатов
    if (!string.IsNullOrEmpty(savePath))
    {
        resultService.SaveResultsToFile(results, savePath);
    }

    loadTestService.Dispose();
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
    AnsiConsole.MarkupLine("  --header, -H        Заголовки в формате 'Name:Value'");
    AnsiConsole.MarkupLine("  --config, -c        Путь к файлу конфигурации (JSON)");
    AnsiConsole.MarkupLine("  --save, -s          Путь для сохранения результатов (CSV)");
    AnsiConsole.MarkupLine("  help                Показать эту справку");
});

return await rootCommand.InvokeAsync(args);
