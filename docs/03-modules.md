# 03. Справочник по модулям

Для каждого модуля: файлы → контракт → обязательные свойства → исключения → тесты.
Пути — от `SpaceBattle/SpaceBattle.Lib/`, тесты — от `SpaceBattle/SpaceBattle.Tests/Tests/`. Общая картина — [02-architecture.md](02-architecture.md), ключи — [04-ioc-keys.md](04-ioc-keys.md). Где модуль лежит по веткам — [01-branches.md](01-branches.md).

Имена команд определяют ключи `Commands.<M(тип)>` (точки в имени типа → `_`).

---
## Часть A. Игровые команды (ствол `foundation/command-infrastructure` и ветки damage / world-update)

### 3.1 Инфраструктура

| Тема | Файлы |
|---|---|
| Интерфейсы | `Interfaces/{ICommand,IGameItem,IGameItemFactory,IGameItemStorage,IBattleContext,IBattleContextFactory,ICommandContext,IExceptionFactory}.cs` |
| Объекты | `GameItems/GameItem.cs`, `Storages/GameItemStorage.cs`, `Factories/*`, `Contexts/{BattleContext,CommandContext}.cs` |
| Значения | `ValueObjects/{Vector,Angle,VelocityCalculation}.cs` |
| Команды | `Commands/{MacroCommand,SetPropertyCommand}.cs` |
| Кодогенерация | `Builders/*`, `Compilers/RoslynSourceCompiler.cs`, `Finders/CommandFinder.cs`, `Inspectors/InterfaceInspector.cs`, `Utilities/{GeneratedNames,CSharpTypeName}.cs`, `Registration/GeneratedStrategyRegistration.cs`, `Dependencies/Dependency.cs` |
| Действия | `Strategies/{ActionCompositionStrategy,ActionConfigurationStrategy}.cs` и регистрации `…ActionComposition`, `…ContextActionComposition`, `…ActionConfigurations`, `…ContextCommand`, `…MacroCommand` |

Тесты: `GameItemFactoryTests`, `GameItemStorageTests`, `BattleContextTests`, `CodeGeneratorTests`, `ActionCompositionTests`, `ContextActionCompositionTests`, `MacroCommandTest`, `VectorTest`, `AngleTest`.

Нюансы:
- `GameItemStorage.Add` с повторным ключом → `ArgumentException`; `Get` несуществующего → `KeyNotFoundException`; `GetSnapshot()` возвращает копию.
- `GameItemFactory.Create(props)` копирует словарь.
- `RegisterIoCDependencyComputedVelocity`: `Vector(round(speed·cos θ), round(speed·sin θ))`, округление от нуля; `speed < 0` → `ArgumentOutOfRangeException`; переполнение → `OverflowException`. Для `Angle(1)` и `speed = 10` получается `(7, 7)`.
- `RegisterIoCDependencyContextCommand(depKey, commandKey, role)` регистрирует ключ, который берёт из `ICommandContext` предмет роли и резолвит `commandKey` с ним; `depKey` и `commandKey` должны различаться.
- `Vector.operator +` с разной размерностью → `ArgumentException`.

### 3.2 Движение и топливо

| | |
|---|---|
| Команды | `MoveCommand(IMoving)`: `Position += Velocity`; `CheckFuelCommand(IFuelCheckable)`: `Fuel < FuelConsumption` → `InvalidOperationException("Not enough fuel to move.")`; `BurnFuelCommand(IFuelBurnable)`: `Fuel -= FuelConsumption` |
| Интерфейсы | `IMoving{Position get/set, Velocity get}`, `IFuelCheckable{Fuel, FuelConsumption}`, `IFuelBurnable{Fuel get/set, FuelConsumption}` |
| Действие | `Config/Movement.config`: Check → Move → Burn (порядок важен) |
| Вызов | `Ioc.Resolve<Lib.ICommand>("Commands.Action", "Movement", ship)` |
| Свойства предмета | `Position`, `Velocity` либо (`Speed` + `Direction` при `MovementDependencies.ComputedVelocity`), `Fuel`, `FuelConsumption` |
| Тесты | `MovementTests`, `MoveCommandTests`, `FuelCommandTests`, `MovementComputedVelocityTests` |

Если топлива не хватает, позиция и топливо не меняются (Check падает первым, макрокоманда прерывается). Нехватка любого свойства → `KeyNotFoundException`, предмет не мутируется.

### 3.3 Вращение

`RotateCommand(IRotating)`: `Direction += AngularVelocity` (по модулю 8). `IRotating{Direction get/set, AngularVelocity get}`. Действие `Rotation` (`Config/Rotation.config`).

### 3.4 Дозаправка

`RefuelCommand(IRefuelTarget target, IRefuelSource source)`: `amount = min(source.RefuelAmount, source.FuelReserve)`; `target.Fuel += amount`; `source.FuelReserve -= amount` (отдаёт остаток, а не падает).
`IRefuelTarget{Fuel get/set}`, `IRefuelSource{FuelReserve get/set, RefuelAmount get}`.
`RegisterIoCDependencyRefuelContextCommand(depKey, targetRole, sourceRole)` (роли должны различаться). `Config/Refuel.config` = одна строка `Context.Refuel`; вызов — `Commands.ContextAction` с `CommandContext({Target: ship, Source: tanker})`. Для адаптеров нужен `GeneratedStrategiesWithAdapters([asm], [typeof(IRefuelTarget), typeof(IRefuelSource)], [])`.

### 3.5 Стрельба

| | |
|---|---|
| Команда | `ShootCommand(IShooting)` |
| Интерфейс | `IShooting{Position, Direction, ProjectileSpeed, ProjectileDamage, Ammo get/set, ProjectileFactory, Storage, ExceptionFactory}`; последние три приходят через `ShootingDependencies.All`, а не из свойств предмета |
| Фабрика | `ProjectileFactory(IGameItemFactory, Func<IGameItem,Vector>)`, ключ `Factories.Projectile` |
| Действие | `Config/Shooting.config` = `Commands.SpaceBattle_Lib_ShootCommand` |
| Ошибки | `Ammo <= 0` → `InvalidOperationException("Not enough ammunition to shoot.")`; в фабрике: `ArgumentNullException`, `ArgumentOutOfRangeException` (скорость/урон < 0), `ArgumentException` (`position.Dim != 2`) |
| Тесты | `ShootingTests`, `ProjectileFactoryTests` |

Снаряд — `IGameItem` со свойствами `Id, Position, Direction, Speed, Damage, Destroyed, Velocity`; кладётся в `Storage` по `Id`, `Ammo` уменьшается на 1. `Ammo` хранится в самом предмете (перенаправления через `Dependency` нет).

### 3.6 Столкновения

Файлы: `Collisions/{CollisionFile,CollisionOffset,CollisionTree}.cs`, `Commands/CollisionCommand.cs`, `Registration/RegisterIoCDependencyCollisions.cs`, `…CollisionContextCommand.cs`, `tools/collisions/*`.
- Ошибка формата строки → `FormatException("Line N: expected 4 integers …")`. Каталог без таблиц → `DirectoryNotFoundException`. Несуществующее дерево → исключение IoC «not found».
- Дерево строится лениво и кэшируется: повторный `Resolve` возвращает тот же объект.
- Контекстная команда: двое по ролям + дерево + имя последствия (`ContextAction`).
- Тесты (в ветках collisions/damage): `CollisionFileTests`, `CollisionTreeTests`, `CollisionCommandTests`, `CollisionRegistrationTests`, `CollisionContextCommandTests`.

### 3.7 Урон (`feature/damage`)

`ApplyDamageCommand(IDamageTarget{Health get/set}, IDamageSource{Damage})`, `DestroyIfDeadCommand(IDestructible{Health, Destroyed get/set})`, `RegisterIoCDependencyDamageContextCommand(depKey, targetRole, sourceRole)`. `Config/Hit.config` = `Context.ApplyDamage`, `Context.DestroyIfDead`.
Полная сборка попадания: `RegisterIoCDependencyCollisionContextCommand("Commands.Hit.Collision", "Collisions.Ship-Torpedo", "Target", "Source", "Hit")`.

### 3.8 Тики и шаг мира

| Класс | Роль |
|---|---|
| `GameTicks : IGameTicks` | вызывает шаг мира с периодом `Step`; `Start()`, `Stop()`, `TickNumber`, `IsRunning`, `Failure` |
| `DelayedCommandScheduler : IDelayedCommandScheduler` | планирует команду через заданное время |
| `DeliverGameTickCommand`, `TickCommand` | доставка тика на поток игры |
| `StartGameTicksCommand`, `StopGameTicksCommand` | команды запуска и остановки тиков |
| `GameTickFactory` | создание `GameTicks`: `GameTicks.Create(step, worldStep, post[, maxCatchUp])` |

Если тик запоздал, выполняется до `maxCatchUpTicks` пропущенных (по умолчанию 2). Проверка аргументов: `step > 0`, `maxCatchUpTicks ≥ 0`.

**Шаг мира** (ветки `world-update`/`game-ticks`):
- `WorldStepFactory` создаёт «кадр» — предмет со свойствами `Battle` и `Items` (снимок хранилища) — и вызывает действие `WorldStep` (`Commands.Action`).
- Фазы задаются JSON: `{"phases":[{"name":"…","execution":"World"|"Items","action":"…","selector":"…"}]}`. `World` — действие над кадром; `Items` — действие для каждого предмета, прошедшего селектор (`Func<IGameItem,bool>` по ключу `selector`); для `World` селектор указывать нельзя.
- Загрузка и регистрация: `RegisterIoCDependencyWorldPhaseConfigurationLoader` → `RegisterIoCDependencyConfiguredWorldPhases(path[, action])`. Дубли имён фаз и подмена `Commands.Action`/`Commands.Macro` запрещены (`FormatException`).
- Возможное ограничение при интеграции: по более раннему аудиту добавление этих веток к серверному стеку упирается в архитектурный тест по папке `Scheduling` (не перепроверялось, уточнить при слиянии).

### 3.9 Объектная авторизация (`Authorization/*`)

| Класс | Роль |
|---|---|
| `AccessAreaTree` | дерево областей: `Add(area, parent?)`, `Move(area, parent?)`, `GetPath(area)`; перенос области ниже самой себя запрещён |
| `ObjectAccessAreas` | привязка предмета к области (по ссылке): `Bind`, `Unbind`, `FindArea` |
| `AccessGrantStore` | выданные разрешения (субъект, действие, область) |
| `InheritedGrantRule` | `Allow`, если разрешение выдано в области предмета или любой родительской; иначе `Abstain` |
| `AuthorizationPolicy` | `IsAllowed(subject, action, item)`: любой запрет — запрет; нужен хотя бы один `Allow` |
| `AuthorizedActionCommand`, `AuthorizedActionFactory` | выполнить действие, если разрешено, иначе вызвать обработчик отказа |

Ключи: `Authorization.Policy`, `Factories.AuthorizedAction`, `Commands.AuthorizedAction(subject, action, item)`. Субъект должен быть **доверенным** (из проверенного токена); к `Authentication/*` не привязано.

---
## Часть B. Серверный стек

### 3.10 Игра

- `Game(id, IGameQueue, IGameScope, IQuantum)`: `Post`, `HasCommands`, `Pending` (команды в очереди), `Execute()`.
- `TimeQuantum(TimeSpan, TimeProvider)`: `duration < 0` → `ArgumentOutOfRangeException`; `Start()` запоминает момент; `IsOver` = прошло `≥ duration`. В тестах время подменяется `ManualTimeProvider`.
- `Games.Create(id)`: свой `BattleContext`, свой IoC-скоуп, своя `Storages.GameItem`.
- `GameRunner`, `PostToGameCommand` — см. [02-architecture.md](02-architecture.md) §6.
- Тесты: `GameTests`, `GameRegistrationTests`, `TimeQuantumTests`, `GameRunnerTests`, `GameRunnerRegistrationTests`.

### 3.11 Серверный поток и очереди

- `ICommandQueue`: `Add`, `AddFirst`, `TryAdd` (с учётом ёмкости), `Take` (блокирует), `TryTake`, `CompleteAdding`, `Count`; после `CompleteAdding` добавление → `InvalidOperationException`.
- `BlockingCommandQueue` — простая FIFO; `PriorityCommandQueue` — по приоритету (`IPrioritizedCommand.Priority`, больше — раньше), внутри приоритета FIFO; `AddFirst` обгоняет всех.
- `QueueScheduler` — FIFO запланированных игр (не потокобезопасен).
- `ServerThread`: `Start`, `Add`, `Interrupt`, обработка исключений. `HardStopCommand`/`SoftStopCommand` проверяют, что выполняются потоком, которому предназначены.
- Ёмкость ящика — `ServerOptions.InboxCapacity`. Когда поток не может принять сообщение, а также когда игра сейчас не в состоянии `Running` или буфер переноса полон, бросается `ServerOverloadedException` (HTTP 503 с `Retry-After: 1`).
- Тесты: `ServerThreadTests`, `ServerThreadRegistrationTests`, `CommandCollectionsTests`, `PriorityQueueTests`.

### 3.12 Маршрутизация

- `IHashFunction.Hash(string) → ulong`; `FnvHashFunction` (по умолчанию), `Sha256HashFunction`.
- `ConsistentHashRing<TNode>(hash, replicas)`: `Add(id, node)` (дубль → `ArgumentException`), `Remove(id) → bool`, `Find(key)`, `Count`.
- `BoundedLoadRouter<TNode>(ring, meter, tolerance, probes)`; `tolerance ≥ 0`, `probes ≥ 1`. `PlacementLoadMeter`, `PlannedMoves`, `PlanningRouter` учитывают игры, которые прямо сейчас переезжают.
- Регистрация: `RegisterIoCDependencyRouting(replicas[, hash])` → `Routing.Ring`, `Routing.Thread(gameId)`; `RegisterIoCDependencyBoundedLoadRouting(tolerance, probes)` подменяет `Routing.Thread` и маршрут ребаланса.
- Тесты: `ConsistentHashRingTests`, `FnvHashFunctionTests`, `Sha256HashFunctionTests`, `RoutingRegistrationTests`, `BoundedLoadRouterTests`, `BoundedLoadRoutingRegistrationTests`, `PlacementLoadMeterTests`, `PlannedMovesTests`.

### 3.13 Сообщения

- `JsonMessageParser.Parse(text)` → `IMessage{GameId, Command, Args}`. Не JSON, не объект, нет строки `gameId`/`command` → `MessageFormatException`.
- `MessageReceiver.Receive(text)`: разбор → поиск игры → `Send`. Неизвестная игра → `KeyNotFoundException("The game "X" is not running on this server.")`.
- `GameRegistry` — `ConcurrentDictionary`; дубль `gameId` → `ArgumentException`.
- `IoCMessageInterpreter` резолвит `Messages.Command.<command>`. Из коробки есть только `CreateItem`; остальные регистрирует приложение.
- Тесты: `JsonMessageParserTests`, `MessageReceiverTests`, `GameRegistryTests`, `MessagesRegistrationTests`, `CommandWhitelistTests`, `MessageDecoratorRegistrationTests`.

### 3.14 Сервер

- `Server` + состояния (`Servers/States/*`) — описаны в [02-architecture.md](02-architecture.md) §9.
- `IServer = IServerLifecycle + IServerThreads + IServerAvailability`.
- Регистрация: `RegisterIoCDependencyServer(threadCount)` → `Server.Instance` и команды `Server.Start`, `Server.SoftStop`, `Server.HardStop` (`Resolve<Lib.ICommand>(key).Execute()`). Команды: `StartServerCommand`, `SoftStopServerCommand`, `HardStopServerCommand`.
- Потребители проверяют состояние через `IsRunning` / `EnsureRunning()`.
- Тесты: `ServerTests`, `ServerRegistrationTests`.

### 3.15 Предметы и снимки

| Файл | Роль |
|---|---|
| `Items/ItemCreator.cs` | `Create(kind, id, props)`; `Id`/`Kind` в `props` запрещены |
| `Items/CreateItemCommand.cs`, `CreateItemMessageCommand.cs` | создание предмета из сообщения `CreateItem` |
| `Serialization/JsonValueCodec.cs` | значения с тегами типов |
| `Serialization/ItemSerializer.cs`, `IoCItemSchema.cs` | предмет ↔ JSON по схеме вида |
| `Serialization/CommandJsonSerializer.cs`, `MessageCommandSerializer.cs` | команды очереди ↔ JSON |
| `Serialization/SnapshotGameCommand.cs`, `RestoreItemsCommand.cs` | снимок и восстановление |

Сообщение `CreateItem`: `{"gameId":"g","command":"CreateItem","args":{"kind":"Ship","id":"s1","properties":{"Fuel":{"type":"int","value":10},"Owner":{"type":"string","value":"p1"}}}}`. Нет `kind` или `id` → `MessageFormatException`.

Тесты: `GameSnapshotTests`, `GameItemCreationTests`, `FileSnapshotStoreTests`.

Регистрации: `RegisterIoCDependencyGameSnapshot` (кодек, типы значений, `Games.Snapshot`, `Games.Restore`), `RegisterIoCDependencyGameItems` (`Items.Create`, `Messages.Command.CreateItem`), `RegisterIoCDependencyItemKind(kind, свойства)`, `RegisterIoCDependencyValueType<T>(тег, write, read)`.

### 3.16 Управляемые игры, перенос, ребаланс, удаление потока

| Класс | Роль |
|---|---|
| `GameRuntime` | фасад: `Start(id)`, `Find`, `Games`, `SnapshotAndStop(store)`, `Restore(store)`, `AddThread`, `Rebalance`, `RemoveThread(id)`, `Transfer(id, destination)`, `Receive(snapshot)`, `Send(message)` |
| `ManagedGameRegistry`, `ManagedGameHost` | реестр игр и хост игры (`State`, `Thread`, `BeginTransfer`, `CommitTransfer`, `CancelTransfer`, `Relocate`) |
| `GameHostLifecycle`, `GameHostTransferService` | состояние и буфер сообщений во время переезда |
| `GameLifecycle` | запуск и восстановление игр |
| `GameSnapshotService` | снимок и восстановление всех игр, `FileSnapshotStore` |
| `GameTransferService` | перенос на другой сервер, `IRemoteServer` |
| `GameRebalanceService` | добавление потока и перераспределение |
| `ThreadRemovalService` | эвакуация игр и удаление потока |

Одновременно допускается одна операция над топологией (замок `lifecycle`); перенос и приём при занятом замке отвечают `ServerOverloadedException`.
Тесты: `GameTransferTests`, `GameTransferFailureTests`, `ThreadRebalanceTests`, `ThreadRemovalTests`, `ServerSnapshotTests`, `ManagedGameValidationTests`, `RuntimeAbstractionTests`, `HostCompositionCoverageTests`, `ServerHostTests`.

### 3.17 Обработка ошибок

- `ErrorAction { Retry, Skip, StopGame, StopThread }`.
- `ExceptionErrorPolicy(rules)`: `Decide(error, attempt)`; правило по точному типу исключения, затем по базовому, иначе `Skip`; `Retry` после исчерпания попыток превращается в `Skip`.
- `ErrorPolicyCommand(action, recovery)`: выполняет действие и при исключении делегирует `IErrorRecovery`. Реализации действий: `RetryErrorAction`, `SkipErrorAction`, `StopGameErrorAction`, `StopThreadErrorAction`, `StopCurrentThreadErrorAction`.
- `ILog` (`MemoryLog`, `LoggingExceptionHandler`, в HTTP — `ApplicationLog`): записывает игру, исключение, решение и номер попытки.
- Ключи: `Errors.Policy`, `Errors.Log`, `Messages.Decorate` (подключение к конвейеру).
- Политика применяется только к командам-сообщениям. Тесты: `ErrorPolicyTests`, `ErrorRecoveryRegressionTests`, `ThreadErrorRecoveryTests`.

### 3.18 HTTP (`SpaceBattle.Http`)

| Эндпоинт | Запрос | Ответы |
|---|---|---|
| `POST /messages` | заголовок `Authorization: Bearer <JWT>`; тело — JSON-сообщение `{gameId, command, args}` | `202` принято; `401` нет или неверен токен; `403` токен другой игры / нет права; `400` неверный формат, JSON или команда не из белого списка; `404` игра не найдена; `503` + `Retry-After: 1` сервер перегружен |
| `POST /games/{id}/transfer` | то же + тело `{"destination": "<имя сервера>"}`; токен должен быть для игры `{id}` и содержать право `transfer` | `204` готово; `401`; `403`; `400`; `404` неизвестный получатель; `502` ошибка связи с получателем; `503` занят |

Имена серверов-получателей приложение задаёт словарём `IReadOnlyDictionary<string, IRemoteServer>` (в `HostComposition` по умолчанию пуст: переносить некуда, пока он не заполнен).
Настройки сервера — секция `Server` в `appsettings.json` (см. [07-playbook.md](07-playbook.md) §1).
Тесты: `HttpApiTests`, `HttpTransferFailureTests`, `ServerLifetimeAbstractionTests` (на базе `Microsoft.AspNetCore.Mvc.Testing`); сквозной тест жизненного цикла — в ветке `test/server-e2e`.

### 3.19 Права игрока (`Authentication/*`)

| Класс | Роль |
|---|---|
| `JwtPlayerTokenValidator` | проверка подписи, срока, издателя, аудитории; результат `PlayerIdentity` |
| `AuthenticatedMessageReceiver` | разбор, сверка игры токена, белый список, отправка |
| `PlayerAuthorizationInterpreter`, `PlayerAuthorizationCommand` | проверка прав перед выполнением команды |
| `PlayerCommandPermissions`, `PlayerItemPermissions` | какие команды и предметы доступны игроку |
| `TargetItemResolver` | достаёт идентификатор предмета из `args` по `CommandTargets` |
| `ExistingItemOwnerRule`, `NewItemOwnerRule` | правила `existing-owner` и `new-owner` |
| `ScopedItemOwners` | определяет владельца предмета по хранилищу игры |
| `OwnerClaim` | читает `args.properties.Owner` (тип `string`) из сообщения `CreateItem` |

Ошибки: `UnauthorizedAccessException` (HTTP 403) для всех отказов. Тесты: `AuthenticationTests`, `PlayerAuthorizationTests`, `AccessRuleConfigurationTests`.
