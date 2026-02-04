# JsonPostClient

Консольное приложение для отправки JSON POST-запроса на указанный эндпоинт. Поддерживает клиентский сертификат для проверки запросов сервером (mTLS).

## Режимы работы

### Интерактивный режим

Запуск без параметров — приложение запросит URL эндпоинта, путь к файлу с JSON, путь к клиентскому сертификату (.pfx) и при необходимости пароль к сертификату:

```bash
dotnet run
```

или после сборки:

```bash
./JsonPostClient
```

### Режим параметров

Передача URL и пути к JSON через аргументы:

```bash
dotnet run -- -url https://example.com/api/endpoint -json ./payload.json
```

или:

```bash
./JsonPostClient -url https://example.com/api/endpoint -json ./payload.json
```

## Отчёт

После выполнения запроса выводится отчёт:

- **Код ответа** — HTTP-статус (200, 404 и т.д.)
- **Время отклика** — длительность запроса в миллисекундах

## Сертификат (mTLS)

Если тестируемый сервер проверяет входящие запросы по клиентскому сертификату, укажите путь к `.pfx` и при необходимости пароль:

```bash
dotnet run -- -url https://api.example.com/ -json data.json -cert ./client.pfx -cert-password "secret"
```

Пароль можно задать переменной окружения `CERT_PASSWORD` (удобно для CI/скриптов):

```bash
set CERT_PASSWORD=secret
JsonPostClient.exe -url https://api.example.com/ -json data.json -cert ./client.pfx
```

## Дополнительные параметры

| Параметр | Описание |
|----------|----------|
| `-url`, `--url` | URL эндпоинта |
| `-json`, `--json` | Путь к файлу с JSON-телом запроса |
| `-cert`, `--cert` | Путь к клиентскому сертификату (.pfx) |
| `-cert-password`, `--cert-password` | Пароль к .pfx (или переменная CERT_PASSWORD) |
| `-k`, `--skip-ssl` | Игнорировать ошибки проверки SSL сервера (только для тестовых окружений) |

## Сборка

```bash
cd JsonPostClient
dotnet build
```

Запуск собранного exe (из папки `bin/Debug/net9.0` или `bin/Release/net9.0`):

```bash
dotnet run
# или
./JsonPostClient
```

## Требования

- .NET 9.0
