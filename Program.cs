using System.CommandLine;
using System.Globalization;
using Spectre.Console;
using ConsoleLoadTesting.Models;
using ConsoleLoadTesting.Services;

public sealed class Program
{
    private static readonly RootCommand _rootCommand = new("Console application for website load testing");

    private static readonly Option<string[]> _urlsOption = new(
        aliases: new[] { "--urls", "-u" },
        description: "URLs to test (you can provide multiple)")
    {
        AllowMultipleArgumentsPerToken = true
    };

    private static readonly Option<string> _urlModeOption = new(
        aliases: new[] { "--mode", "-m" },
        description: "URL selection mode: sequential or random",
        getDefaultValue: () => "sequential");

    private static readonly Option<int> _usersOption = new(
        aliases: new[] { "--users", "-v" },
        description: "Number of virtual users",
        getDefaultValue: () => 1);

    private static readonly Option<int> _requestsOption = new(
        aliases: new[] { "--requests", "-r" },
        description: "Requests per user",
        getDefaultValue: () => 1);

    private static readonly Option<int> _delayOption = new(
        aliases: new[] { "--delay", "-d" },
        description: "Delay between requests in milliseconds",
        getDefaultValue: () => 0);

    private static readonly Option<int> _maxInFlightPerUserOption = new(
        aliases: new[] { "--inflight-per-user", "--max-inflight-per-user" },
        description: "Max in-flight requests per virtual user (1 = sequential per user)",
        getDefaultValue: () => 1);

    private static readonly Option<string[]> _headersOption = new(
        aliases: new[] { "--header", "-H" },
        description: "Request headers in 'Name:Value' format (you can provide multiple)")
    {
        AllowMultipleArgumentsPerToken = true
    };

    private static readonly Option<string?> _configOption = new(
        aliases: new[] { "--config", "-c" },
        description: "Path to config file (JSON)");

    private static readonly Option<string?> _saveOption = new(
        aliases: new[] { "--save", "-s" },
        description: "Path to save results CSV file");

    private static readonly Option<string[]> _loadOption = new(
        aliases: new[] { "--load", "-l" },
        description: "Paths to result files to analyze (you can provide multiple)")
    {
        AllowMultipleArgumentsPerToken = true
    };

    private static readonly Option<int> _chartTimeStepSeconds = new(
        aliases: new[] { "--chart-step" },
        description: "Chart time step in seconds",
        getDefaultValue: () => 1);

    public static async Task<int> Main(string[] args)
    {
        SetCulture();
        ConfigureRootCommand();

        try
        {
            if (args.Length == 0)
            {
                await _rootCommand.InvokeAsync(new[] { "--help" }).ConfigureAwait(false);
                return 0;
            }

            await _rootCommand.InvokeAsync(args).ConfigureAwait(false);
            return 0;
        }
        catch (Exception ex)
        {
            _ = await ErrorLogService.WriteAsync(ex).ConfigureAwait(false);
            return 1;
        }
    }

    private static void ConfigureRootCommand()
    {
        _rootCommand.AddOption(_urlsOption);
        _rootCommand.AddOption(_urlModeOption);
        _rootCommand.AddOption(_usersOption);
        _rootCommand.AddOption(_requestsOption);
        _rootCommand.AddOption(_delayOption);
        _rootCommand.AddOption(_maxInFlightPerUserOption);
        _rootCommand.AddOption(_headersOption);
        _rootCommand.AddOption(_configOption);
        _rootCommand.AddOption(_saveOption);
        _rootCommand.AddOption(_loadOption);
        _rootCommand.AddOption(_chartTimeStepSeconds);

        _rootCommand.SetHandler(async (context) =>
        {
            var parseResult = context.ParseResult;
            var commandContext = new CommandContext
            {
                Urls = parseResult.GetValueForOption(_urlsOption) ?? Array.Empty<string>(),
                UrlMode = parseResult.GetValueForOption(_urlModeOption) ?? "sequential",
                Users = parseResult.GetValueForOption(_usersOption),
                Requests = parseResult.GetValueForOption(_requestsOption),
                Delay = parseResult.GetValueForOption(_delayOption),
                MaxInFlightPerUser = parseResult.GetValueForOption(_maxInFlightPerUserOption),
                Headers = parseResult.GetValueForOption(_headersOption) ?? Array.Empty<string>(),
                ConfigPath = parseResult.GetValueForOption(_configOption) ?? string.Empty,
                SavePath = parseResult.GetValueForOption(_saveOption) ?? string.Empty,
                LoadPaths = parseResult.GetValueForOption(_loadOption) ?? Array.Empty<string>(),
                ChartTimeStepSeconds = parseResult.GetValueForOption(_chartTimeStepSeconds)
            };

            await Process(commandContext, context.GetCancellationToken()).ConfigureAwait(false);
        });
    }

    private static async Task Process(CommandContext context, CancellationToken token)
    {
        var resultService = new ResultService();

        if (context.LoadPaths.Length > 0)
        {
            resultService.DisplaySummaryReport(context.LoadPaths, context.ChartTimeStepSeconds);
            return;
        }

        var configService = new ConfigService();
        var loadTestService = new LoadTestService();
        var resultServiceForTest = new ResultService();

        var config = BuildConfig(context, configService);

        AnsiConsole.MarkupLine("[bold green]Starting load test...[/]");
        AnsiConsole.WriteLine();

        var outputPath = FileService.ResolveOutputPath(
            string.IsNullOrWhiteSpace(context.SavePath) ? null : context.SavePath,
            "ConsoleLoadTesting",
            ".csv");

        var realtimeStats = new RealtimeStats();
        var statsLock = new object();
        var startTime = DateTime.UtcNow;

        AnsiConsole.MarkupLine($"[grey]Results file: {outputPath}[/]");

        await using var resultWriter = new ResultWriter(outputPath);

        try
        {
            await AnsiConsole.Live(resultServiceForTest.CreateUrlStatsTable(realtimeStats, config.Urls, startTime))
                .StartAsync(async liveCtx =>
                {
                    var onResultReceived = new Action<TestResult>(result =>
                    {
                        lock (statsLock)
                        {
                            realtimeStats.Add(result);
                            liveCtx.UpdateTarget(resultServiceForTest.CreateUrlStatsTable(realtimeStats, config.Urls, startTime));
                        }
                    });

                    await loadTestService.RunLoadTestAsync(
                        config,
                        null,
                        onResultReceived,
                        resultWriter,
                        token).ConfigureAwait(false);
                }).ConfigureAwait(false);
        }
        finally
        {
            await resultWriter.CompleteAsync().ConfigureAwait(false);
            loadTestService.Dispose();
        }

        AnsiConsole.WriteLine();
        resultServiceForTest.DisplayResultsFromFile(outputPath, config.ChartTimeStepSeconds);
    }

    private static TestConfig BuildConfig(CommandContext context, ConfigService configService)
    {
        TestConfig config;

        if (!string.IsNullOrWhiteSpace(context.ConfigPath))
        {
            config = configService.LoadFromFile(context.ConfigPath);
        }
        else if (context.Urls.Length > 0)
        {
            config = new TestConfig
            {
                Urls = context.Urls.ToList(),
                UrlMode = context.UrlMode.Equals("random", StringComparison.OrdinalIgnoreCase)
                    ? UrlMode.Random
                    : UrlMode.Sequential,
                VirtualUsers = context.Users,
                RequestCount = context.Requests,
                DelayMs = context.Delay,
                MaxInFlightPerUser = context.MaxInFlightPerUser,
                ChartTimeStepSeconds = context.ChartTimeStepSeconds
            };

            ParseHeaders(context.Headers, config.Headers);
        }
        else
        {
            throw new ArgumentException("No input provided. Use --help for usage.");
        }

        ValidateConfigOrThrow(config);
        return config;
    }

    private static void ValidateConfigOrThrow(TestConfig config)
    {
        if (config.Urls.Count == 0)
        {
            throw new ArgumentException("No URLs provided for testing");
        }

        if (config.VirtualUsers < 1)
        {
            throw new ArgumentException("Virtual users must be greater than 0");
        }

        if (config.RequestCount < 1)
        {
            throw new ArgumentException("Request count must be greater than 0");
        }

        if (config.MaxInFlightPerUser < 1)
        {
            throw new ArgumentException("MaxInFlightPerUser must be greater than 0");
        }
    }

    private static void ParseHeaders(string[] headers, Dictionary<string, string> targetDict)
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

    private static void SetCulture()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
    }

    private sealed class CommandContext
    {
        public string[] Urls { get; init; } = Array.Empty<string>();
        public string UrlMode { get; init; } = "sequential";
        public int Users { get; init; } = 1;
        public int Requests { get; init; } = 1;
        public int Delay { get; init; }
        public int MaxInFlightPerUser { get; init; } = 1;
        public string[] Headers { get; init; } = Array.Empty<string>();
        public string ConfigPath { get; init; } = string.Empty;
        public string SavePath { get; init; } = string.Empty;
        public string[] LoadPaths { get; init; } = Array.Empty<string>();
        public int ChartTimeStepSeconds { get; init; } = 1;
    }
}
