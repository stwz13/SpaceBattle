# 05. Ограничения кода, тесты и CI

Проверено по деревьям веток на 2026-11-09. Правила работы с пользовательским клоном — в [AGENTS.md](../AGENTS.md), команды песочницы — в [07](07-playbook.md).

## 1. Архитектурные ограничения

`ArchitectureConstraintsTests` разбирает C# через Roslyn:

- Во всех исходниках `SpaceBattle.Lib` рекурсивно, кроме `bin`/`obj`, запрещены токены `for` и `while` (включая `do … while`). Разрешены `foreach`, LINQ, рекурсия.
- В `Commands`, `ValueObjects`, `Threads`, `Receivers`, `Processors`, `Behaviours`, `Queues` запрещены токены `Ioc` и `IoC`. Тест перечисляет непосредственно файлы каждой указанной папки; правило проекта следует соблюдать и во вложенных папках.
- Глобального запрета на `new` в актуальном trunk нет. Старые foundation/refuel могут содержать более ранние ограничения; проверять тест конкретной ветки.
- `test/server-e2e` дополнительно вводит `ServerRuntimeArchitectureTests`: в `Runtime`, `Authentication`, `Scheduling` запрещён прямой IoC и создание объектов, кроме явно создаваемых исключений; запрещён и неявный `new(...)`. Зависимости, коллекции, фабрики и стратегии передаются из регистраций.

В свежем серверном стеке выделены узкие интерфейсы жизненного цикла, реестра, снимков, переноса, балансировки, удаления потока и error recovery. IoC-сборка — `Registration/*`, точка композиции приложения — `ServerComposition`, HTTP — отдельный проект. Не переносить service locator в предметные команды.

Два `ICommand`: игровой `SpaceBattle.Lib.ICommand`, контейнерный `App.ICommand`. Регистрация/установка скоупа исполняется через `Ioc.Resolve<App.ICommand>(...).Execute()`.

## 2. Соглашения и различия веток

- Nullable и implicit usings включены; язык по умолчанию SDK .NET 10. Общий namespace большинства классов Lib — `SpaceBattle.Lib` независимо от подпапки.
- Ключи брать из `*Dependencies`; динамические имена — через `GeneratedNames`, `Schema`, `CommandKey` и имеющиеся функции.
- Генератор автоматически находит только публичные команды с суффиксом `Command` и единственным параметром-конструктором — адаптируемым интерфейсом. Интерфейс содержит только свойства, без методов, generic, индексаторов и событий.
- Контекстные команды над двумя объектами регистрируются явно; нужные адаптеры добавляются через `WithAdapters`.
- `.config` определяет порядок шагов. Ошибка останавливает макрокоманду, отката уже выполненных шагов нет. Не обобщать «объект не изменился» на любую ошибку всей цепочки.
- `RegisterIoCDependencyGeneratedStrategies` в актуальном trunk делегирует `GeneratedStrategyRegistration`: прежнее описание двух дублирующих реализаций устарело.
- В trunk кэш `RoslynSourceCompiler` ещё статический; в `shooting` и свежем серверном стеке — словарь экземпляра, внедряемый через конструктор. Есть `CompilerCacheIsolationTests`. При слиянии не терять исправление.
- Обычные `GameQueue`/`QueueScheduler` используются из потока-владельца. Потокобезопасность inbox не делает безопасным прямое изменение game storage из HTTP-потока.
- Вместимость inbox проверяется в `TryAdd`; служебные `Add`/`AddFirst` могут обходить лимит. Stop-команды доставляются и при перегрузке.
- Управляющие операции managed runtime координируются через lifecycle semaphore; скоупы и контекст исполнения потока нельзя произвольно заменять при рефакторинге.

## 3. Проверки

В основных ветках: xUnit 2.9.3, Moq 4.20.72, Microsoft.NET.Test.Sdk 17.14.1, coverlet.collector 6.0.4. HTTP-тесты используют `Microsoft.AspNetCore.Mvc.Testing` 10.0.14 и `WebApplicationFactory<Program>`; отдельный внешний сервер не требуется. JWT-ветки добавляют `Microsoft.IdentityModel.JsonWebTokens` 8.23.0. Точные версии смотреть в `.csproj` соответствующей ветки.

В scratch-копии (PowerShell):

```powershell
$env:DOTNET_CLI_UI_LANGUAGE = 'en'
$env:VSLANG = '1033'
dotnet restore SpaceBattle/SpaceBattle.slnx
dotnet build SpaceBattle/SpaceBattle.slnx -c Release --no-restore
dotnet test SpaceBattle/SpaceBattle.Tests/SpaceBattle.Tests.csproj -c Release --no-build `
  --collect:'XPlat Code Coverage' --logger 'trx;LogFileName=tests.trx' `
  --results-directory ./results
```

Результат привязывать к SHA. Покрытие Cobertura — метрика инструментированных сборок, а не доказательство выполнения требований преподавателя. В аудите SDK 10.0.300; также установлен 10.0.103. Свежая таблица — [06](06-status.md).

Старые предупреждения `xUnit1031` от блокирующих ожиданий не следует выдавать за текущие: в game/server-thread/game-runner/messages/server-management добавлены async-исправления тестов. В аудите встречается `NU1900`: данные NuGet vulnerability audit не получены, хотя restore/build/test завершаются; повтор restore вне песочницы также дал предупреждение. Успешные тесты не означают успешную проверку уязвимостей.

## 4. CI

`.github/workflows/ci.yml` имеет один и тот же git blob во всех 37 проверенных снимках (включая удалённую за время аудита refuel); обе преподавательские DLL также одинаковы во всех этих деревьях:


1. Только событие `pull_request`, целевые ветки `['**']`; отдельного `push`-триггера нет.
2. `ubuntu-latest`, `actions/checkout@v4`, `setup-dotnet@v4` / `10.0.x`, кэш NuGet по `.csproj`.
3. Restore решения, Release build, test с `--no-build` и `XPlat Code Coverage`.
4. `irongut/CodeCoverageSummary@v1.3.0`: `thresholds: "70 90"`, Markdown/бейдж; `fail_below_min` в YAML не задан. Это не объявленный в проекте жёсткий coverage gate.
5. Комментарий в PR через `marocchino/sticky-pull-request-comment@v2`; `contents: read`, `pull-requests: write`.

Статус реальных GitHub Actions по PR в данном аудите не проверялся. Локальные прогоны не обозначаются как «CI зелёный».
