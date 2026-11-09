# 05. Правила кода, тесты, CI

## 1. Жёсткие ограничения (проверяет `ArchitectureConstraintsTests`, ломают сборку PR)

Тест разбирает исходники `SpaceBattle.Lib/**/*.cs` (без `obj/` и `bin/`).

| Тест | Правило | Что делать |
|---|---|---|
| `Source_Has_No_For_Or_While_Loops` | в любом файле Lib запрещены `for` и `while` (и `do … while`) | `foreach`, LINQ (`Select`, `Where`, `TakeWhile`, `Aggregate`), `Enumerable.Range(0, int.MaxValue).TakeWhile(...)`, рекурсия |
| `Subject_Code_Has_No_Direct_IoC_Access` | в папках `Commands`, `ValueObjects`, `Threads`, `Receivers`, `Processors`, `Behaviours`, `Queues` нет токенов `Ioc`/`IoC` | зависимости приходят через конструктор; с контейнером работают `Registration/`, `Strategies/`, `Messages/`, `Scopes/`, `Games/`, `Runtime/` и т. д. |

Новые «предметные» папки лучше добавить в список теста, чем обходить правило.
Бывшее правило «`new` только для `Vector`/`Angle`» снято. Тем не менее по принятому в проекте соглашению **объекты создаются в стратегиях IoC и фабриках**, а в предметных классах `new` не нужен; в регистрациях и при создании исключений он допустим.

## 2. Соглашения по коду

- `namespace SpaceBattle.Lib;` (file-scoped) в Lib, `namespace SpaceBattle.Tests;` в тестах. Один тип на файл, имя файла = имя типа.
- Раскладка папок Lib: `Interfaces/`, `Commands/`, `Registration/` (`RegisterIoCDependency<Что> : ICommand`), `Dependencies/<Что>Dependencies.cs` (константы ключей), `Utilities/DependencyNamespaces.<X>.cs` (partial с префиксом), `Config/*.config`; предметные папки: `Games/`, `Threads/`, `Servers/`, `Routing/`, `Messages/`, `Receivers/`, `Runtime/`, `Serialization/`, `Items/`, `Authentication/`, `Scheduling/`, `Collections/` и др.
- Команда принимает **один** интерфейс-представление (тогда `CommandFinder` её найдёт) либо работает над несколькими предметами через `ICommandContext` с ручной регистрацией.
- Конструкторы проверяют аргументы: `ArgumentNullException.ThrowIfNull`, `ArgumentException.ThrowIfNullOrWhiteSpace`, `ArgumentOutOfRangeException.ThrowIf…`.
- Стратегии IoC принимают `object[] args`, сами проверяют их число и типы и бросают `ArgumentException` с понятным текстом.
- Новое поведение — новый класс и регистрация, старый код не правят (открытость/закрытость). Интерфейсы узкие: потребитель зависит только от нужного (`IServerAvailability`, `IServerThreads`, …).
- Свежий код использует primary-конструкторы и collection expressions (`[]`).
- Имена тестов: `Что_Происходит_При_Каких_Условиях`.

## 3. Git и pull request

- **Conventional Commits**: `feat(server): …`, `fix(routing): …`, `test(game-runner): …`, `refactor(…): …`, `chore: …`.
- **В дифф PR нет удалённых строк.** PR только добавляет. Правка кода, добавленного этим же PR, вносится в тот коммит, где код появился (`git commit --fixup <sha>` → `git rebase -i --autosquash`).
- PR идут **стеком**; после правки нижней ветки перебазируйте верхние: `git rebase --onto <новая-основа> <старая-основа> <ветка>`, затем `git push --force-with-lease`.
- Чужой код (например, `IExceptionFactory`) без согласования с автором не менять.
- Окончания строк приводятся `core.autocrlf=true`.

## 4. Тесты

- Пакеты: xUnit 2.9.3, Moq 4.20.72, coverlet.collector 6.0.4, `Microsoft.NET.Test.Sdk` 17.14.1, `Microsoft.AspNetCore.Mvc.Testing` 10.0.14 (HTTP).
- Структура: конструктор тест-класса поднимает чистый IoC-скоуп и регистрирует нужное (xUnit создаёт экземпляр на каждый тест — изоляция). Кэш Roslyn делает повторную компиляцию дешёвой.
- Заглушки — `TestDoubles/`: `TestGameItem`, `RuntimeFixture`, `GameApiFactory`, `ManualTimeProvider`, `FakeHashFunction`, `RecordingExceptionHandler`, `SpyCommandQueue`, `StubServerThread` и др.
- Конфигурационные файлы берутся из `Path.Combine(AppContext.BaseDirectory, "Config")` либо пишутся во временный каталог.
- **Не блокируйте задачи в тестах** (`.Wait()`, `.Result`, `Task.WaitAll`): используйте `async Task` и `await task.WaitAsync(timeout)`. Иначе анализатор xUnit1031 выдаёт предупреждения.
- **IoC-скоуп привязан к потоку** (`ThreadLocal`). После `await` тест может оказаться на другом потоке и потерять скоуп: делайте обращения к `Ioc` до `await` или внутри `ScopeExecution.InScope`.
- Цель — покрытие каждого изменения выше 90 %.

Запуск:
```bash
cd SpaceBattle
dotnet restore SpaceBattle.slnx
dotnet build   SpaceBattle.slnx -c Release --no-restore
dotnet test    SpaceBattle.Tests/SpaceBattle.Tests.csproj -c Release --no-build \
               --collect:"XPlat Code Coverage" --results-directory ./coverage
# английский вывод на русской Windows:
DOTNET_CLI_UI_LANGUAGE=en VSLANG=1033 dotnet test ...
```

## 5. CI (`.github/workflows/ci.yml`)

- Запускается **только на `pull_request`** (для любых веток). На `push` CI не запускается.
- `ubuntu-latest`, `actions/setup-dotnet@v4` (`10.0.x`), кэш NuGet, `dotnet restore` / `build -c Release` по `SpaceBattle/SpaceBattle.slnx`, `dotnet test … --collect:"XPlat Code Coverage"`.
- Отчёт покрытия: `irongut/CodeCoverageSummary` (пороги «70 90») и комментарий в PR. Пороги влияют на цвет отчёта, **сборку не валят**, но цель команды — выше 90 %.
- Права: `contents: read`, `pull-requests: write`.
- Бинарные `Assemblies/*.dll` лежат в репозитории (без них CI не соберётся).

## 6. Частые ловушки

1. Два `ICommand`: `App.ICommand` (с префиксом) и `Lib.ICommand`.
2. `CommandFinder` находит только команды с **одним** адаптируемым параметром и именем на `Command`. `RefuelCommand(target, source)` и `ApplyDamageCommand` ей не видны: адаптеры заказывают явно (`WithAdapters`), команды создают `…ContextCommand`-регистрации.
3. Адаптируемый интерфейс — **только свойства**. Нельзя адаптировать интерфейс с методами (`IProjectileFactory`): такие зависимости подаются через `Dependency`.
4. Порядок шагов в `.config` — порядок выполнения; отката нет.
5. `Angle` — восьмые доли: `new Angle(9) == new Angle(1)`. `Vector` сравнивается по значению, но это класс.
6. `GameQueue` и `QueueScheduler` не потокобезопасны. Потокобезопасны: очереди ящика, `GameRegistry`, `ConsistentHashRing`.
7. Регистрации сервера привязаны к скоупу, текущему на момент `Execute()`.
8. Кэш Roslyn общий на процесс; первый тест с новым набором интерфейсов заметно дольше.
9. Игры и сервер запоминают скоуп: при вызове из другого потока оборачивайте обращения в `ScopeExecution.InScope(scope, …)`.
10. В снимок попадают только команды-сообщения: любая другая команда в очереди ломает снимок (`InvalidOperationException`).
11. Топологические операции (перенос, ребаланс, удаление потока) взаимно исключаются: при занятости — `ServerOverloadedException`.
12. Вывод `dotnet` на русской локали: для `grep` по «Passed!» используйте `DOTNET_CLI_UI_LANGUAGE=en VSLANG=1033`.
