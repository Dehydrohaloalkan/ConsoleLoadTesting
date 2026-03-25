using System.CommandLine;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using Spectre.Console;
using JsonPostClient.Services;

var urlOption = new Option<string?>(
    aliases: new[] { "--url", "-url" },
    description: "Endpoint URL for the POST request");

var jsonOption = new Option<string?>(
    aliases: new[] { "--json", "-json" },
    description: "Path to file with JSON request body");

var certOption = new Option<string?>(
    aliases: new[] { "--cert", "-cert" },
    description: "Path to client certificate (.pfx) for server verification");

var certPasswordOption = new Option<string?>(
    aliases: new[] { "--cert-password", "-cert-password" },
    description: "Password for .pfx (or CERT_PASSWORD env var)");

var skipSslOption = new Option<bool>(
    aliases: new[] { "--skip-ssl", "-k" },
    description: "Ignore server SSL validation errors (testing only)",
    getDefaultValue: () => false);

var rootCommand = new RootCommand("Send a JSON POST request to an endpoint");
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
        endpointUrl = AnsiConsole.Ask<string>("Endpoint URL:");
        if (string.IsNullOrWhiteSpace(endpointUrl))
        {
            AnsiConsole.MarkupLine("[red]URL is required.[/]");
            context.ExitCode = 1;
            return;
        }
        jsonFilePath = AnsiConsole.Ask<string>("Path to JSON file:");
        if (string.IsNullOrWhiteSpace(jsonFilePath))
        {
            AnsiConsole.MarkupLine("[red]Path to JSON is required.[/]");
            context.ExitCode = 1;
            return;
        }
        certPath = AnsiConsole.Ask<string>("Path to client certificate (.pfx):");
        if (string.IsNullOrWhiteSpace(certPath))
        {
            AnsiConsole.MarkupLine("[red]Path to certificate is required.[/]");
            context.ExitCode = 1;
            return;
        }
        certPassword ??= AnsiConsole.Ask<string>("Certificate password (press Enter if none):");
    }
    else
    {
        AnsiConsole.MarkupLine("[red]Please provide both parameters: -url and -json[/]");
        context.ExitCode = 1;
        return;
    }

    if (!Uri.TryCreate(endpointUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https" && uri.Scheme != "http")
    {
        AnsiConsole.MarkupLine("[red]Invalid URL.[/]");
        context.ExitCode = 1;
        return;
    }

    var jsonContent = await File.ReadAllTextAsync(jsonFilePath).ConfigureAwait(false);
    if (string.IsNullOrWhiteSpace(jsonContent))
    {
        AnsiConsole.MarkupLine("[red]JSON file is empty.[/]");
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
            AnsiConsole.MarkupLine($"[red]Certificate file not found: {certPathResolved}[/]");
            context.ExitCode = 1;
            return;
        }
        var cert = X509CertificateLoader.LoadPkcs12FromFile(certPathResolved, certPassword ?? string.Empty);
        handler.ClientCertificates.Add(cert);
    }

    using var client = new HttpClient(handler);
    var result = await PostService.SendJsonAsync(client, endpointUrl, jsonContent).ConfigureAwait(false);

    AnsiConsole.MarkupLine("[bold]Report[/]");
    AnsiConsole.MarkupLine($"  [cyan]Status code:[/] {result.StatusCode}");
    AnsiConsole.MarkupLine($"  [cyan]Response time:[/] {result.ResponseTimeMs} ms");
    if (!string.IsNullOrEmpty(result.ErrorMessage))
        AnsiConsole.MarkupLine($"[red]Error: {result.ErrorMessage}[/]");

    context.ExitCode = result.StatusCode >= 200 && result.StatusCode < 300 ? 0 : 1;
});

return await rootCommand.InvokeAsync(args).ConfigureAwait(false);
