using Spectre.Console;
using ConsoleLoadTesting.Models;

namespace ConsoleLoadTesting.Services;

public class InteractiveService
{
    public TestConfig GetConfigInteractively()
    {
        var config = new TestConfig();

        AnsiConsole.MarkupLine("[bold cyan]Интерактивный режим настройки нагрузочного тестирования[/]");
        AnsiConsole.WriteLine();

        // Ссылки
        AnsiConsole.MarkupLine("[yellow]Введите URL-адреса для тестирования (по одному на строку, пустая строка для завершения):[/]");
        while (true)
        {
            AnsiConsole.Write("URL (или Enter для завершения): ");
            var url = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(url))
            {
                if (config.Urls.Count == 0)
                {
                    AnsiConsole.MarkupLine("[red]Необходимо указать хотя бы один URL![/]");
                    continue;
                }
                break;
            }
            if (Uri.TryCreate(url, UriKind.Absolute, out _))
            {
                config.Urls.Add(url);
                AnsiConsole.MarkupLine($"[green]✓ Добавлен URL: {url}[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Некорректный URL![/]");
            }
        }

        // Режим работы с ссылками
        var urlModeChoice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Режим работы с ссылками:")
                .AddChoices("Последовательно", "Случайно")
        );
        config.UrlMode = urlModeChoice == "Последовательно" ? UrlMode.Sequential : UrlMode.Random;

        // Количество виртуальных пользователей
        config.VirtualUsers = AnsiConsole.Ask<int>("Количество виртуальных пользователей:", 1);
        if (config.VirtualUsers < 1) config.VirtualUsers = 1;

        // Количество запросов
        config.RequestCount = AnsiConsole.Ask<int>("Количество запросов на пользователя:", 1);
        if (config.RequestCount < 1) config.RequestCount = 1;

        // Задержка между запросами
        config.DelayMs = AnsiConsole.Ask<int>("Задержка между запросами (мс):", 0);
        if (config.DelayMs < 0) config.DelayMs = 0;

        // Заголовки
        AnsiConsole.MarkupLine("[yellow]Добавить заголовки? (y/n)[/]");
        if (AnsiConsole.Confirm("Добавить заголовки?", false))
        {
            while (true)
            {
                AnsiConsole.Write("Имя заголовка (или Enter для завершения): ");
                var headerName = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(headerName))
                    break;

                AnsiConsole.Write("Значение заголовка: ");
                var headerValue = Console.ReadLine() ?? string.Empty;
                config.Headers[headerName] = headerValue;
                AnsiConsole.MarkupLine($"[green]✓ Добавлен заголовок: {headerName}[/]");
            }
        }

        return config;
    }
}
