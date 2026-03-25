# Документация

Этот репозиторий содержит **несколько .NET-проектов**:

- `ConsoleLoadTesting` — основное приложение для нагрузочного тестирования.
- `JsonPostClient` — отдельная утилита для отправки JSON POST (включая mTLS и опцию `--skip-ssl` для тестовых окружений).

## Важно про сборку

По умолчанию SDK-проекты .NET рекурсивно подхватывают `**/*.cs` в каталоге проекта.  
Чтобы `ConsoleLoadTesting` **не компилировал исходники подпроекта** `JsonPostClient` (включая `JsonPostClient/obj/**`), в `ConsoleLoadTesting.csproj` явно исключены файлы из `JsonPostClient/**`.

## Быстрый старт

- Собрать основное приложение:

```bash
dotnet build .\ConsoleLoadTesting.csproj
```

## Зависимости

- UI/консольный вывод: `Spectre.Console` (в проекте закреплено на версии `0.54.0`)

- Собрать утилиту `JsonPostClient`:

```bash
dotnet build .\JsonPostClient\JsonPostClient.csproj
```

- Запустить `ConsoleLoadTesting` (пример):

```bash
dotnet run --project .\ConsoleLoadTesting.csproj -- --urls https://example.com --users 5 --requests 10
```

Дополнительные примеры и описание параметров см. в корневом `README.md` и `JsonPostClient/README.md`.

## Потоковая запись результатов

В `ConsoleLoadTesting` результаты теперь пишутся потоково в CSV:

- у каждого виртуального пользователя есть локальный буфер емкостью `min(100, requests)`;
- заполненный буфер передается отдельному writer-потоку;
- writer-поток последовательно записывает пачки в итоговый CSV-файл;
- если путь через `--save` не задан, создается temp-файл с датой в имени;
- итоговая статистика и графики после теста читаются из файла буферизованно, без `ReadAllLines` и без хранения всех результатов в памяти.

