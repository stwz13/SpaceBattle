# 02. Архитектура

Язык и платформа: C# 14, **.NET 10** (`net10.0`), `Nullable=enable`, `ImplicitUsings=enable`.
Пакеты: Roslyn (`Microsoft.CodeAnalysis.CSharp` 4.14.0) для генерации адаптеров, `Microsoft.IdentityModel.JsonWebTokens` 8.23.0 для JWT, ASP.NET Core в `SpaceBattle.Http`. Тесты: xUnit 2.9.3, Moq 4.20.72, coverlet.

Описание относится к серверному стеку (`feature/server-host` и выше) и к стволу игровых команд. Где что лежит — [01-branches.md](01-branches.md).

## 1. Сборки и проекты

```
SpaceBattle.slnx
├── Assemblies/core.dll      App.Ioc, App.ICommand, UpdateIocResolveDependencyStrategyCommand (готовая, исходников нет)
├── Assemblies/scopes.dll    App.Scopes.InitCommand, DependencyResolver (готовая, исходников нет)
├── SpaceBattle.Lib          вся логика
├── SpaceBattle.Http         ASP.NET Core: точка входа и два HTTP-эндпоинта
└── SpaceBattle.Tests        xUnit; ссылается на Lib, Http и обе DLL
```

`core.dll` и `scopes.dll` менять нельзя.

### Два разных `ICommand`

- `App.ICommand` — из `core.dll`; только IoC возвращает его (`IoC.Register`, `IoC.Scope.Current.Set`). Пишется **всегда с префиксом**.
- `SpaceBattle.Lib.ICommand` — доменный интерфейс; все команды игры реализуют его. В коде Lib без префикса `ICommand` означает именно его. В тестах (namespace `SpaceBattle.Tests`) пишут `Lib.ICommand`.

Это самая частая ошибка в проекте.

## 2. IoC и скоупы

Всё получают через `Ioc.Resolve<T>(ключ, аргументы...)`. Стратегия по ключу — `Func<object[], object>`: она сама проверяет число и тип аргументов и бросает `ArgumentException`.

Ключи самого контейнера (из `scopes.dll`; семантика выведена из использования):

| Ключ | Аргументы | Результат |
|---|---|---|
| `IoC.Register` | `key`, `Func<object[],object>` | `App.ICommand`; `.Execute()` регистрирует стратегию в **текущем** скоупе |
| `IoC.Scope.Create` | – | новый дочерний скоуп |
| `IoC.Scope.Create.Empty` | – | скоуп без родителя |
| `IoC.Scope.Current` | – | текущий скоуп |
| `IoC.Scope.Current.Set` | `scope` | `App.ICommand`, делает скоуп текущим |
| `IoC.Scope.Current.Clear` | – | `App.ICommand` |
| `IoC.Scope.Parent` | – | родитель; у корневого бросает исключение |

Неизвестный ключ → `Exception("Dependency … is not found.")`.

**Текущий скоуп хранится отдельно для каждого потока** (`ThreadLocal`). Поэтому после `await` код, оказавшийся на другом потоке, видит чужой скоуп. Выход — `ScopeExecution.InScope(scope, () => ...)` и `GameScope`: `GameScope.Enter()` запоминает текущий скоуп, ставит свой и возвращает `IDisposable`, который при `Dispose` всё возвращает.

Регистрации **скоупозависимы**: стратегия, зарегистрированная в скоупе игры, не видна снаружи. Регистрацию в корне делает `ServerComposition`, а у каждой игры свой дочерний скоуп.

## 3. Модель данных

- `IGameItem` — словарь свойств: `GetProperty(name)`, `SetProperty(name, value)`. Отсутствующее свойство → `KeyNotFoundException`. Типы значений по соглашению: `Position`/`Velocity` — `Vector`, `Direction`/`AngularVelocity` — `Angle`, `Fuel`/`Speed`/`Ammo`/`Health`/`Damage` — `int`, `Destroyed` — `bool`.
- `Vector` — неизменяемый целочисленный вектор (`Coordinates`, `Dim`, оператор `+`, сравнение по значению).
- `Angle` — угол в **восьмых долях круга**: `Numerator` нормализуется в `[0, 8)`, `new Angle(9) == new Angle(1)`.
- `IGameItemStorage` (`Add`, `Get`, `Remove`, `GetSnapshot`) — хранилище предметов игры, ключ — строковый `Id`. Дубль ключа → `ArgumentException`.
- `IBattleContext` = `Id` + `Storage`. Создаётся `IBattleContextFactory.Create(id)`.
- `ICommandContext` — «роль → предмет» для команд над несколькими предметами (Target, Source).

### Предметы с `Id` и `Kind`

`ItemCreator.Create(kind, id, properties)`:
1. запрещает `Id` и `Kind` в `properties` (иначе `ArgumentException`);
2. добавляет их сам;
3. создаёт предмет через `IGameItemFactory`;
4. кладёт в хранилище **текущей игры** под ключом `id`.

Идентификатор уникален в пределах одной игры. `Kind` нужен для снимка: он указывает, какие свойства предмета сохранять (схема вида).

Известное ограничение: `GameItemStorage.Add` не проверяет, что ключ совпадает со свойством `Id` предмета.

## 4. Команды над адаптерами и кодогенерация

Команды работают не с `IGameItem`, а с узким интерфейсом-представлением (`IMoving`, `IRotating`, `IShooting` …). Класс-адаптер от `IGameItem` к такому интерфейсу **генерируется Roslyn-ом во время выполнения**.

Конвейер (`RegisterIoCDependencyGeneratedStrategies`):

1. `CommandFinder.Find(assembly)` ищет публичные неабстрактные классы, реализующие `Lib.ICommand`, с именем на `Command` и **ровно одним** параметром конструктора — адаптируемым интерфейсом.
2. `InterfaceInspector.IsAdaptable`: интерфейс публичный, не generic, **только свойства** (ни методов, ни событий, ни индексаторов).
3. `AdapterSourceBuilder` генерирует класс `SpaceBattle.Generated.<Имя>Adapter`, свойства которого ходят в IoC: `get` → `Ioc.Resolve<T>("Properties.<вид>.<объявляющий тип>.<Имя>", item)`, `set` → `Ioc.Resolve<ICommand>("Properties….<Имя>.Set", item, value).Execute()`.
4. `StrategySourceBuilder` генерирует статический класс стратегий (адаптер + команда вокруг него).
5. `RoslynSourceCompiler` компилирует в память и кэширует по тексту исходника на весь процесс.
6. Регистрация в текущем скоупе: `Adapters.<M(интерфейс)>Adapter`, `Commands.<M(команда)>`, `Properties.…` (геттер) и `Properties.….Set` (команда `SetPropertyCommand`).

`M(тип)` = `FullName` с заменой `.` и `+` на `_`. Пример: `Commands.SpaceBattle_Lib_MoveCommand`.

### `Dependency` — свойства, которых нет в предмете

`Dependency(Type Interface, string Property, string Strategy)` говорит генератору: «геттер этого свойства не читай из `IGameItem`, а возьми из IoC по ключу `Strategy`». Примеры: `IMoving.Velocity` ← `Properties.ComputedVelocity` (скорость вычисляется из `Speed` и `Direction`), `IShooting.ProjectileFactory` ← `Factories.Projectile`.

### Варианты регистрации

| Класс | Назначение |
|---|---|
| `RegisterIoCDependencyGeneratedStrategies(Assembly, deps)` | находит команды и интерфейсы сам |
| `RegisterIoCDependencyGeneratedStrategiesWithAdapters(assemblies, extraInterfaces, deps)` | плюс адаптеры для интерфейсов без одноимённой команды (команды над двумя предметами) |
| `RegisterIoCDependencyGeneratedAdapters(interfaces, deps)` | только адаптеры |

Требует предварительно `RegisterIoCDependencyCodeGeneration`.

## 5. Действия (Actions)

Действие — именованная последовательность команд из файла `Config/<Action>.config`. Формат: токены (имена IoC-ключей) через перевод строки или запятую, всё после `#` — комментарий.

```
# Movement.config
Commands.SpaceBattle_Lib_CheckFuelCommand
Commands.SpaceBattle_Lib_MoveCommand
Commands.SpaceBattle_Lib_BurnFuelCommand
```

- `RegisterIoCDependencyActionConfigurations(dir)` регистрирует для каждого `*.config` ключи `<Action>.Config` и `Specs.<Action>`.
- `Commands.Action(action, item)` резолвит каждое имя с `item` и оборачивает всё в `Commands.Macro` (`MacroCommand`).
- `Commands.ContextAction(action, ICommandContext)` — то же, но шаги резолвятся с контекстом (для команд над двумя предметами; шаги вида `Context.Xxx`).
- Макрокоманда выполняет шаги по порядку; исключение на любом шаге **прерывает** остаток (поэтому «нет топлива» не даёт двигаться — проверка идёт первой). Отката уже выполненных шагов нет.
- Создание действия его не выполняет; повторный `Execute()` читает актуальные свойства.

## 6. Игра

Игра (`Game`) — очередь команд (`GameQueue`), скоуп (`GameScope`) и квант времени (`TimeQuantum`).

`Game.Execute()`: войти в скоуп игры → `quantum.Start()` → выполнять команды из очереди, пока они есть и квант не истёк (одна команда за заход выполняется всегда, чтобы игра не зависла).

- `GameQueue` не потокобезопасна: касаться её можно только из потока игры.
- `Games.Create(id)` создаёт игру со своим `BattleContext`, своим скоупом и своим хранилищем предметов.
- `Game.Execute` исключения не перехватывает. Их ловит серверный поток (см. §8), а команды-сообщения дополнительно оборачивает политика ошибок (см. §13).

`GameRunner(game, thread)`: `Post(cmd)` кладёт команду в игру и один раз ставит себя в расписание потока; `Execute()` прогоняет игру, а в `finally` ставит себя заново, если в игре остались команды. Так несколько игр на одном потоке получают кванты по очереди.

## 7. Серверный поток

`ServerThread` сам является командой. Запуск — `Task.Factory.StartNew(Run, LongRunning)`, повторный старт → исключение. У потока:

- **входящий ящик** (`ICommandQueue`) — в серверных ветках приоритетная очередь `PriorityCommandQueue` (по умолчанию включена, `ServerOptions.PriorityQueues`), внутри одного приоритета порядок FIFO;
- **планировщик** (`QueueScheduler`) — команды игр, которые надо выполнить;
- обработчик исключений `IExceptionHandler`: `Handle` ловит исключение любой команды и передаёт обработчику (в сервере это `LoggingExceptionHandler`).

Остановка:
- `SoftStop` — закрыть ящик и **дорабатывать** накопленное;
- `HardStop` — команда обгоняет очередь (`AddFirst`), остальное отбрасывается.

Обе команды выполняются только самим потоком, иначе `InvalidOperationException`.

## 8. Маршрутизация

`ConsistentHashRing<TNode>` — кольцо с виртуальными узлами. Ключ — `gameId`, узел — `IServerThread`. Позиции узла считаются как `hash("{id}#{replica}")`. Хэш по умолчанию — `FnvHashFunction` (есть и `Sha256HashFunction`). Пустое кольцо → `InvalidOperationException("The ring has no nodes.")`.

### Ограничение нагрузки (`feature/bounded-load-routing`)

Обычное кольцо может посадить несколько тяжёлых игр на один поток. `BoundedLoadRouter` оставляет владельца с кольца, пока на нём игр меньше `ceil((игр + 1) / потоков × (1 + tolerance))`; иначе пробует ключ `key/1`, `key/2`, … (до `LoadProbes` попыток), а если все заняты — берёт наименее загруженный. Включается опцией `ServerOptions.LoadTolerance`. Нагрузку считает `PlacementLoadMeter` по играм, живущим на каждом потоке, включая переносимые (`PlannedMoves`). Тот же маршрут используют ребаланс и удаление потока.

## 9. Сервер и его состояния

`Server` владеет потоками и кольцом. Его поведение вынесено в **состояния** (паттерн «Состояние»), перечисления `ServerState` нет:

| Состояние | Класс | Поведение |
|---|---|---|
| создан | `CreatedServerState` | `Start` (создаёт начальные потоки и запускает), `AddThread` (поток создаётся, но не запускается до `Start`), остановка |
| работает | `RunningServerState` | `AddThread` (поток сразу запускается), остановка; повторный `Start` → `InvalidOperationException` |
| закрыт | `ClosedServerState` | всё запрещено: `Start` → «already been started», `AddThread` → «The server is stopping.», остановка → «already been stopped» |

Создан и работает наследуют `OpenServerState`: остановка у них общая — перейти в `Closed` и закрыть потоки (`Shutdown`).

`ServerStateBase.EnsureRunning()` бросает «The server is not running.», если сервер не работает. Состояния — одиночки (`Instance`). Переход делает сам сервер через `IServerContext.Become(...)`.

Интерфейс сервера разделён по назначению (разделение интерфейсов, SOLID):

| Интерфейс | Что даёт |
|---|---|
| `IServerLifecycle` | `Start`, `SoftStop`, `HardStop` |
| `IServerThreads` | `ThreadCount`, `AddThread` |
| `IServerAvailability` | `IsRunning`, `EnsureRunning()` |
| `IServer` | объединяет все три |

Потребитель зависит только от нужного интерфейса: например, перенос игры — от `IServerAvailability`, ребаланс — от `IServerThreads`.

`SoftStop`/`HardStop` убирают потоки из кольца, шлют стоп-команды и возвращают `Task`, завершающийся, когда все потоки закончили. Пока потоки не закончили, сервер уже не «работает» (`IsRunning == false`).

## 10. Сообщения

Формат: `{"gameId": "<строка>", "command": "<строка>", "args": <любой JSON, необязателен>}`. Нарушение формата → `MessageFormatException`.

Цепочка в серверном приложении (по HTTP):

1. `ApiEndpoints` проверяет JWT (§12) и получает `PlayerIdentity`.
2. `AuthenticatedMessageReceiver.Receive(text)`: разбор → `message.GameId == player.GameId` (иначе `UnauthorizedAccessException`) → белый список (§12) → `GameRegistry.Find(gameId).Send(authenticated)`.
3. `GameHost` → `PostToGameCommand(runner, InterpretMessageCommand(interpreter, message))` → в ящик потока.
4. Поток выполняет игру, `InterpretMessageCommand` получает интерпретатор из конвейера и вызывает `Messages.Command.<имя>` с сообщением.

Конвейер интерпретатора собирается декораторами: базовый `IoCMessageInterpreter` → проверка прав игрока (`PlayerAuthorizationInterpreter`) → политика ошибок (`PolicyMessageInterpreter`). Ключ декоратора — `Messages.Decorate`.

## 11. Снимки, перенос, ребаланс, удаление потока

### Снимок игры (JSON)

```json
{
  "gameId": "demo",
  "items": [
    { "id": "s1", "kind": "Ship",
      "properties": { "Fuel": {"type": "int", "value": 10}, "Position": {"type": "vector", "value": [12, 5]} } }
  ],
  "commands": [ { "type": "message", "gameId": "demo", "command": "…", "args": {…} } ]
}
```

- `JsonValueCodec` кодирует каждое значение как `{"type": <тег>, "value": …}`. Теги зарегистрированы в `RegisterIoCDependencyGameSnapshot`: `int`, `long`, `double`, `bool`, `string`, `angle` (хранится числитель), `vector` (массив). Новый тип — `RegisterIoCDependencyValueType<T>(тег, write, read)`.
- Какие свойства предмета писать, определяет схема вида `Serialization.Schema.<kind>` (`IoCItemSchema`). Новый вид — `RegisterIoCDependencyItemKind(kind, свойства)`.
- Из очереди сохраняются **только команды-сообщения** (`MessageCommandSerializer`, тег `message`). Любая другая команда в очереди → `InvalidOperationException` («cannot be serialized»); значение неподдерживаемого типа → `NotSupportedException`; неизвестный тег при чтении → `FormatException`.
- Восстановление (`Games.Restore`): разбор → `Games.Create` → пост `RestoreItemsCommand` → пост сохранённых команд.

`SnapshotGameCommand` выполняется на потоке игры, результат отдаёт через приёмник (`sink`). Снимок всего сервера делает `GameSnapshotService`; хранит `ISnapshotStore`: `FileSnapshotStore` пишет файл (`ServerOptions.SnapshotPath`), ключ `Snapshots.Memory` даёт хранилище в памяти.

### Состояния управляемой игры

`GameHostState`: `Running` → `Moving` (идёт перенос или перемещение между потоками) → `Moved` (игра ушла) / `Stopped`. Пауза и буфер сообщений реализованы в `GameHostLifecycle`.

### Перенос игры на другой сервер (`GameTransferService`)

1. Сервер берёт замок управления топологией. Если он занят — `ServerOverloadedException` («занято»), чтобы встречные переносы не зависли.
2. Игра ставится на паузу и снимается в снимок; входящие сообщения **буферизуются** (лимит — ключ `Games.TransferBufferCapacity`; сервер задаёт его равным `InboxCapacity`).
3. Приёмник создаёт игру из снимка на паузе (`Receive`).
4. `CommitTransfer`: сервер-источник пересылает буфер (через прокси-хост) и помечает игру `Moved`.
5. При любой ошибке перенос отменяется (`CancelTransfer`, `Abort`), игра продолжает работать на старом месте.

Протокол между серверами — интерфейс `IRemoteServer` (`Send`, `Transfer`, `Receive`); `GameRuntime` сам его реализует.

### Ребаланс и удаление потока

- `AddThread` добавляет поток и вызывает ребаланс: для каждой запущенной игры считается новый поток по маршруту (§8), и если он другой, игра переезжает (снимок на паузе → восстановление на целевом потоке → `Relocate`).
- `RemoveThread(id)` убирает поток из кольца, эвакуирует его игры тем же способом, затем останавливает поток. Параллельно с ребалансом это невозможно: общий замок `lifecycle`; пока идёт удаление, `AddThread`/`Stop` отвечают «Thread removal is in progress.».

## 12. Безопасность

**JWT** (`RegisterIoCDependencyJwtAuthentication`):
- алгоритм HS256; ключ — минимум 32 байта UTF-8 (`ServerOptions.JwtSecret`); проверяются издатель, аудитория, подпись, срок (`RequireExpirationTime`, без запаса по часам);
- утверждения: `sub` — идентификатор игрока, `game` — идентификатор игры, `permission = transfer` — право переносить игру;
- результат: `PlayerIdentity(PlayerId, GameId, CanTransfer)`.

**Белый список** (`CommandWhitelist`): сообщение с командой не из списка `AllowedCommands` отклоняется (`MessageFormatException` → HTTP 400).

**Права игрока** (`PlayerAuthorizationCommand`, перед выполнением команды):
- игроку разрешены команды из `Players.<id>.Commands` и предметы из `Players.<id>.Items` (`*` — все);
- `CommandTargets` — для каждой команды имя поля в `args`, где лежит идентификатор целевого предмета (например, `"CreateItem": "id"`);
- правила доступа к предмету: `existing-owner` (по умолчанию: свойство `Owner` предмета равно игроку) и `new-owner` (для `CreateItem`: в `args.properties.Owner` указан тот же игрок). Свои правила регистрируются как `Auth.AccessRule.<имя>`, привязываются через `CommandAccessRules`.

**Объектная авторизация** (`Authorization/*`, ствол): `AccessAreaTree` — дерево областей, `ObjectAccessAreas` — привязка предмета к области, `AccessGrantStore` — выданные разрешения, `InheritedGrantRule` — разрешает, если разрешение выдано в области предмета или в любой родительской. `AuthorizationPolicy` складывает правила (запрет любого правила — запрет; нужен хотя бы один `Allow`). `AuthorizedActionCommand` выполняет действие только при разрешении, иначе зовёт обработчик отказа. С `Authentication/*` не связана.

## 13. Обработка ошибок

Три уровня:

1. **Серверный поток**: `ServerThread.Handle` ловит любое исключение команды и передаёт в `IExceptionHandler` (журнал). Поток продолжает работать.
2. **Игра**: `Game.Execute` не ловит ничего; `GameRunner` через `finally` продолжает расписание.
3. **Политика для команд-сообщений** (`ErrorPolicyCommand`): по типу исключения выбирается действие — `Retry` (с лимитом повторов), `Skip` (по умолчанию), `StopGame`, `StopThread`. Решения пишутся в `ILog`. Подходящее правило ищется сначала по точному типу, затем по базовому. Политика действует только на команды, пришедшие сообщениями.

## 14. Запуск серверного приложения

`ServerComposition.Create(options, snapshots?, configureCommands?, log?)` — единственная точка сборки сервера:
1. проверяет `ThreadCount`, `InboxCapacity`, `RingReplicas`, `QuantumMilliseconds`;
2. создаёт скоуп и регистрирует: `ServerFoundation` (фабрики, игра, поток, маршрутизация, сообщения, сервер), `ServerRuntime` (снимки, предметы, виды предметов, управляемые игры, перенос, ребаланс, ограничение нагрузки, удаление потока), `ServerSecurity` (JWT, белый список, правила доступа);
3. вызывает `configureCommands` — здесь приложение регистрирует свои `Messages.Command.<имя>`;
4. регистрирует `ServerPolicies` (права игроков, политика ошибок, обработка ошибок потока);
5. возвращает `ServerApplication`.

`ServerApplication.Start()`: запускает сервер → при `RestoreOnStart` восстанавливает игры из снимка → запускает игры из `Games`. При ошибке мягко останавливает сервер.
`ServerApplication.Stop()` (однократно): при `SnapshotOnStop` — снимок всех игр и остановка, иначе `SoftStop`.

HTTP (`SpaceBattle.Http`): `Program` собирает хост, `HostComposition` читает секцию `Server` из `appsettings.json`, регистрирует сервер и `ServerLifetime` (запуск/остановка вместе с хостом). Эндпоинты — в [03-modules.md](03-modules.md) §3.18.

## 15. Игровые команды (ствол)

### Столкновения
- Таблица — файл `*.collisions`: строки `dx dy dvx dvy`, где `d = B.pos − A.pos`, `dv = B.vel − A.vel`.
- `CollisionTree` — дерево решений по четырём числам, строится рекурсивно.
- `RegisterIoCDependencyCollisions(dir)` для каждого файла регистрирует ленивое дерево `Collisions.<путь>` и фабрику `Commands.Collision`.
- `CollisionCommand(tree, offset, onCollision)` вычисляет смещение **в момент `Execute`**.
- Генератор таблиц: `tools/collisions/generate_collisions.py`.

### Урон (`feature/damage`)
`ApplyDamageCommand(target, source)`: `Health = max(0, Health − Damage)`. `DestroyIfDeadCommand`: `Health ≤ 0 → Destroyed = true`. Цепочка `Hit` (`Config/Hit.config`): `Context.ApplyDamage` → `Context.DestroyIfDead`.

### Тики и шаг мира
`GameTicks` вызывает шаг мира с фиксированным периодом через `IDelayedCommandScheduler`, доставляя команды на поток игры (`DeliverGameTickCommand`). Если шаг отстаёт, выполняется до `maxCatchUpTicks` (по умолчанию 2) пропущенных тиков. Старт/остановка — команды `Commands.GameTicks.Start/Stop`. В ветке `world-update` шаг мира собирается из фаз (`WorldStep`), которые описаны в JSON (`phases[]` с полями `name`, `execution` = `World` или `Items`, `action`, `selector`; для `Items` селектор обязателен).
