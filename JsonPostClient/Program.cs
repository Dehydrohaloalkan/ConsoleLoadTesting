using System.CommandLine;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using Spectre.Console;
using JsonPostClient.Services;

var urlOption = new Option<string?>(
    aliases: new[] { "--url", "-url" },
    description: "URL эндпоинта для POST-запроса");

var jsonOption = new Option<string?>(
    aliases: new[] { "--json", "-json" },
    description: "Путь к файлу с JSON-телом запроса");

var certOption = new Option<string?>(
    aliases: new[] { "--cert", "-cert" },
    description: "Путь к клиентскому сертификату (.pfx) для проверки сервером");

var certPasswordOption = new Option<string?>(
    aliases: new[] { "--cert-password", "-cert-password" },
    description: "Пароль к .pfx (или переменная CERT_PASSWORD)");

var skipSslOption = new Option<bool>(
    aliases: new[] { "--skip-ssl", "-k" },
    description: "Игнорировать ошибки проверки SSL сервера (только для тестов)",
    getDefaultValue: () => false);

var rootCommand = new RootCommand("Отправка JSON POST-запроса на эндпоинт");
rootCommand.AddOption(urlOption);
rootCommand.AddOption(jsonOption);
rootCommand.AddOption(certOption);
rootCommand.AddOption(certPasswordOption);
rootCommand.AddOption(skipSslOption);

rootCommand.SetHandler(async (context) =>
{
    var url = context.ParseResult.GetValueForOption(urlOption);
    var jsonPath = context.ParseResult.GetValueForOption(jsonOption);
    var certPath = context.ParseResult.GetValueForOption(certOption);
    var certPassword = context.ParseResult.GetValueForOption(certPasswordOption)
        ?? Environment.GetEnvironmentVariable("CERT_PASSWORD");
    var skipSsl = context.ParseResult.GetValueForOption(skipSslOption);

    string endpointUrl;
    string jsonFilePath;

    if (!string.IsNullOrWhiteSpace(url) && !string.IsNullOrWhiteSpace(jsonPath))
    {
        endpointUrl = url.Trim();
        jsonFilePath = jsonPath.Trim();
    }
    else if (string.IsNullOrWhiteSpace(url) && string.IsNullOrWhiteSpace(jsonPath))
    {
        endpointUrl = AnsiConsole.Ask<string>("URL эндпоинта:");
        if (string.IsNullOrWhiteSpace(endpointUrl))
        {
            AnsiConsole.MarkupLine("[red]URL не указан.[/]");
            context.ExitCode = 1;
            return;
        }
        jsonFilePath = AnsiConsole.Ask<string>("Путь к файлу с JSON:");
        if (string.IsNullOrWhiteSpace(jsonFilePath))
        {
            AnsiConsole.MarkupLine("[red]Путь к JSON не указан.[/]");
            context.ExitCode = 1;
            return;
        }
        certPath = AnsiConsole.Ask<string>("Путь к клиентскому сертификату (.pfx):");
        if (string.IsNullOrWhiteSpace(certPath))
        {
            AnsiConsole.MarkupLine("[red]Путь к сертификату не указан.[/]");
            context.ExitCode = 1;
            return;
        }
        certPassword ??= AnsiConsole.Ask<string>("Пароль к сертификату (Enter если нет):");
    }
    else
    {
        AnsiConsole.MarkupLine("[red]Укажите оба параметра: -url и -json[/]");
        context.ExitCode = 1;
        return;
    }

    if (!Uri.TryCreate(endpointUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https" && uri.Scheme != "http")
    {
        AnsiConsole.MarkupLine("[red]Некорректный URL.[/]");
        context.ExitCode = 1;
        return;
    }

    var jsonContent = await File.ReadAllTextAsync(jsonFilePath).ConfigureAwait(false);
    if (string.IsNullOrWhiteSpace(jsonContent))
    {
        AnsiConsole.MarkupLine("[red]Файл JSON пуст.[/]");
        context.ExitCode = 1;
        return;
    }

    using var handler = new HttpClientHandler();
    if (skipSsl)
        handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;

    if (!string.IsNullOrWhiteSpace(certPath))
    {
        var certPathResolved = Path.GetFullPath(certPath);
        if (!File.Exists(certPathResolved))
        {
            AnsiConsole.MarkupLine($"[red]Файл сертификата не найден: {certPathResolved}[/]");
            context.ExitCode = 1;
            return;
        }
        var cert = X509CertificateLoader.LoadPkcs12FromFile(certPathResolved, certPassword ?? string.Empty);
        handler.ClientCertificates.Add(cert);
    }

    using var client = new HttpClient(handler);
    var result = await PostService.SendJsonAsync(client, endpointUrl, jsonContent).ConfigureAwait(false);

    AnsiConsole.MarkupLine("[bold]Отчёт[/]");
    AnsiConsole.MarkupLine($"  [cyan]Код ответа:[/] {result.StatusCode}");
    AnsiConsole.MarkupLine($"  [cyan]Время отклика:[/] {result.ResponseTimeMs} мс");
    if (!string.IsNullOrEmpty(result.ErrorMessage))
        AnsiConsole.MarkupLine($"[red]Ошибка: {result.ErrorMessage}[/]");

    context.ExitCode = result.StatusCode >= 200 && result.StatusCode < 300 ? 0 : 1;
});

return await rootCommand.InvokeAsync(args).ConfigureAwait(false);
