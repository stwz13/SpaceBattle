# 07. Playbook: работа с проектом

Срез 2026-11-09. Пользовательский клон — `C:\Users\Maksim\Desktop\оаип\SpaceBattle`. Его текущая ветка main; инструкции/документация включены в git после аудита. Без отдельной просьбы не изменять исходники, ветки и refs, не коммитить и не пушить. Команды ниже с checkout/merge/build/test выполняются **только в scratch-клоне**.

## 1. Безопасная песочница (PowerShell)

```powershell
$sourceRepo = 'C:\Users\Maksim\Desktop\оаип\SpaceBattle'
$scratchRoot = Join-Path $env:TEMP ('spacebattle-' + [Guid]::NewGuid().ToString('N'))
$scratchRepo = Join-Path $scratchRoot 'repo'
New-Item -ItemType Directory -Path $scratchRoot | Out-Null
git clone --no-hardlinks $sourceRepo $scratchRepo
# Локальный clone не переносит исходные remote-tracking refs:
git -C $scratchRepo fetch $sourceRepo '+refs/remotes/origin/*:refs/remotes/origin/*'
# Проверить актуальность, не меняя refs пользовательского клона:
git -C $sourceRepo ls-remote --heads origin
# Если SHA отличаются, обновлять origin в scratch-копии:
git -C $scratchRepo remote set-url origin https://github.com/stwz13/SpaceBattle
git -C $scratchRepo fetch origin --prune
Set-Location $scratchRepo
git checkout --detach origin/test/server-e2e
$env:DOTNET_CLI_UI_LANGUAGE = 'en'
$env:VSLANG = '1033'
dotnet test SpaceBattle/SpaceBattle.Tests/SpaceBattle.Tests.csproj -c Release `
  --collect:'XPlat Code Coverage' --logger 'trx;LogFileName=tests.trx' `
  --results-directory (Join-Path $scratchRoot 'results')
```

Для воспроизводимости сохранять полный SHA и результаты вне рабочей папки сборки. Приведённые в [06](06-status.md) результаты относятся к tips из [аудита refs](audit/branches-2026-11-09.json), не к будущим изменениям GitHub. Для изолированной сборки без checkout допустим `git archive <SHA>` с распаковкой в отдельную папку; именно так проверялись все ветки текущего аудита.

## 2. Проверка состава интеграции

В scratch-клоне:

```powershell
git config user.name 'Scratch integration'
git config user.email 'scratch@example.invalid'
git checkout --detach origin/foundation/command-infrastructure
$integrationBranches = @(
  'test/server-e2e', 'feature/shooting', 'feature/movement-fuel',
  'feature/rotation', 'feature/tanker-refuel', 'feature/damage',
  'feature/world-update', 'feature/object-authorization'
)
foreach ($branch in $integrationBranches) {
  git merge --no-ff -m "Scratch integration: $branch" "origin/$branch"
  if ($LASTEXITCODE -ne 0) { throw "Conflict in $branch; inspect before continuing" }
}
dotnet test SpaceBattle/SpaceBattle.Tests/SpaceBattle.Tests.csproj -c Release
```

На SHA аудита все слияния чистые, но **809/810 тестов**: ServerRuntimeArchitectureTests запрещает `new Delivery` в Scheduling/DelayedCommandScheduler из тиков. Не выдавать состав за готовую совместимую сборку. Для интеграции тиков нужно отдельно согласовать архитектуру создания scheduler deliveries; код в рамках документационного аудита не менялся.

Чтобы воспроизвести проверенный зелёный состав **729/729**, начинать с чистого scratch-клона и исключить `feature/world-update` из массива слияний. В этом составе нет и game-ticks; остальные перечисленные ветки включены. Не заменять этим решением требования пользователя к итоговой функциональности.

Историческая refuel удалена на GitHub к концу аудита, но ещё доступна в исходном локальном refs/remotes/origin/feature/refuel; после prune она может пропасть из refs.

Дополнение bounded-load вызывает конфликты ServerComposition.cs и ServerApplication.cs; refuel — четыре конфликта с tanker-refuel. Полный список — [01](01-branches.md) §5. Повторные слияния messages/game/game-runner не нужны: это предки E2E. Скрипт не публикует результат и не создаёт пользовательских веток.

## 3. Запуск HTTP-host

Нужна scratch-копия `feature/server-host`, `feature/player-authorization` или `test/server-e2e`. `feature/http-api` — более ранняя стадия без полной композиции HostComposition.

Минимальная конфигурация для E2E-ветки с одним игроком и созданием объекта (ключ заменить своим, минимум 32 UTF-8 байта):

```powershell
$env:Server__JwtSecret = '<your-signing-key-at-least-32-UTF8-bytes>'
$env:Server__Games__0 = 'demo'
$env:Server__AllowedCommands__0 = 'CreateItem'
$env:Server__ItemKinds__Ship__0 = 'Fuel'
$env:Server__ItemKinds__Ship__1 = 'Owner'
$env:Server__CommandTargets__CreateItem = 'id'
$env:Server__Players__p1__Commands__0 = 'CreateItem'
$env:Server__Players__p1__Items__0 = 'ship'
dotnet run --project SpaceBattle/SpaceBattle.Http --urls http://localhost:5080
```

Используются appsettings проекта (включая snapshot/restore flags); проверить SnapshotPath и желаемое восстановление перед запуском. Команда выше — рецепт по исходникам, отдельный интерактивный запуск с сетью в аудите не выполнялся; endpoints проверены тестовыми hosts.

JWT получать у внешнего доверенного issuer: HS256, корректные issuer/audience/exp, `sub=p1`, `game=demo`; endpoint выдачи токенов здесь отсутствует. `permission=transfer` требуется только для переноса. Тело POST /messages:

```json
{
  "gameId": "demo",
  "command": "CreateItem",
  "args": {
    "id": "ship",
    "kind": "Ship",
    "properties": {
      "Fuel": { "type": "int", "value": 10 },
      "Owner": { "type": "string", "value": "p1" }
    }
  }
}
```

Заголовок `Authorization: Bearer <token>`. 202 означает приём, последующая ошибка команды обрабатывается политикой ошибок. Для `POST /games/demo/transfer` нужна внедрённая в ASP.NET DI таблица `IReadOnlyDictionary<string,IRemoteServer>`; по умолчанию она пуста. Встроенного сетевого поиска/клиента remote server нет.

Для реальных Move/Shooting/WorldStep мало слить исходники: нужны регистрация генерации и dependency mappings, конфиги действий, команды протокола, схемы Kind, права игроков и подключение tick delivery. ProjectileFactory не добавляет Kind, поэтому снимки снарядов требуют доработки интеграции.

## 4. Новое действие

1. `Interfaces/IFoo.cs`: публичный негeneric интерфейс, только свойства. Тип свойства может иметь методы (например, IProjectileFactory), само адаптируемое представление — нет.
2. `Commands/FooCommand.cs`: ICommand, один параметр IFoo для автопоиска; без IoC и for/while.
3. `Config/Foo.config`: шаги по именам `GeneratedNames.CommandDependency(...)`; конфиги копируются Lib.csproj в output. Ошибка шага останавливает дальнейшие, отката нет.
4. Для вычисляемых/сервисных свойств задать `Dependency(typeof(IFoo), nameof(IFoo.Prop), key)` и зарегистрировать стратегию. Для нескольких объектов — явные adapters и контекстная команда.
5. Изолированный скоуп → CodeGeneration → GeneratedStrategies/WithAdapters → Macro → ActionComposition/ContextActionComposition → ActionConfigurations. Регистрации зависимостей нужны к моменту обращения к ним.
6. Проверить значимое поведение команды и всю ветку `dotnet test`; при изменении общего кода учитывать ветки-потомки и свежий ServerRuntimeArchitectureTests.

## 5. Два объекта и контекст

Два интерфейса Target/Source → команда с двумя параметрами → `GeneratedStrategiesWithAdapters([asm], [typeof(ITarget),typeof(ISource)], deps)` → специальный `RegisterIoCDependency*ContextCommand`. Конфиг действия содержит Context.*; вызов `Commands.ContextAction`, имя действия, ICommandContext. Образцы — tanker-refuel и damage. CommandFinder такую команду автоматически не обнаруживает.

## 6. Регистрация и скоупы

`RegisterIoCDependencyXxx.Execute()` регистрирует стратегии в текущем скоупе через IoC.Register/App.ICommand. В свежем стеке общий helper — IoCRegistrar.Register, временное выполнение в скоупе — ScopeExecution.InScope. ManagedGames создаёт дочерний текущий скоуп, ServerComposition восстанавливает исходный в finally.

Глобальные зависимости сервера и игровые storage не смешивать. Из HTTP передавать сообщение/делегат в game host, не менять storage прямо. Ключи, возвращающие Task (transfer/rebalance/snapshot), уже запускают работу при Resolve — нужно await; базовые Server.Start/Stop возвращают ICommand и требуют Execute.

## 7. Перед сдачей

Сначала получить задание/критерии преподавателя. Выбрать состав веток и вариант refuel, решить обнаруженные интеграционные ограничения, проверить схемы снимков и прикладные команды. Запустить Release build/test с фиксацией SHA; отдельно проверять CI конкретного PR. Не объявлять готовой сдачу только по высокому покрытию или отсутствию конфликтов git.
