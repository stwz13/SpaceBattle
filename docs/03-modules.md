# 03. Справочник по модулям (контракты и поведение)

Срез 2026-11-09; состав веток и SHA — [01](01-branches.md), свежие тесты — [06](06-status.md). Числа старого аудита не переносятся на новые tips.

Каждый раздел: **ветка** → файлы → контракт → обязательные свойства `IGameItem` → исключения → тесты.
Пути — от `SpaceBattle/SpaceBattle.Lib/` (если не указано `Tests/`). Общая модель — [02-architecture.md](02-architecture.md), ключи — [04-ioc-keys.md](04-ioc-keys.md).
Имена команд определяют IoC-ключи `Commands.<Mangled>` (точки → `_`), см. архитектуру §4.

---
## 3.1 Foundation (`foundation/command-infrastructure`, trunk)

| Тема | Файлы |
|---|---|
| Базовые интерфейсы | `Interfaces/ICommand.cs`, `IGameItem.cs`, `IGameItemFactory.cs`, `IGameItemStorage.cs`, `IBattleContext(.Factory).cs`, `ICommandContext.cs`, `IExceptionFactory.cs` |
| Объекты | `GameItems/GameItem.cs`, `Storages/GameItemStorage.cs`, `Factories/{GameItemFactory,BattleContextFactory,ExceptionFactory}.cs`, `Contexts/{BattleContext,CommandContext}.cs` |
| Значения | `ValueObjects/Vector.cs`, `Angle.cs` |
| Команды | `Commands/MacroCommand.cs`, `SetPropertyCommand.cs` |
| Кодогенерация | `Builders/*`, `Compilers/RoslynSourceCompiler.cs`, `Finders/CommandFinder.cs`, `Inspectors/InterfaceInspector.cs`, `Utilities/{GeneratedNames,CSharpTypeName}.cs`, `Registration/GeneratedStrategyRegistration.cs`, `Registration/RegisterIoCDependencyGenerated*.cs`, `Dependencies/Dependency.cs` |
| Действия | `Strategies/{ActionCompositionStrategy,ActionConfigurationStrategy}.cs`, `Registration/RegisterIoCDependency{ActionComposition,ContextActionComposition,ActionConfigurations,ContextCommand,MacroCommand}.cs` |
| Вычисляемая скорость | `Registration/RegisterIoCDependencyComputedVelocity.cs`, `Dependencies/ComputedVelocityDependencies.cs` |

Нюансы:
- `GameItemStorage.Add` с дублирующимся id → `ArgumentException` (поведение `Dictionary.Add`); `Get` несуществующего → `KeyNotFoundException`; `GetSnapshot()` возвращает **копию** (read-only).
- `GameItemFactory.Create(props)` копирует словарь (`StringComparer.Ordinal`).
- `RegisterIoCDependencyComputedVelocity(key="Properties.ComputedVelocity", speedProp="Speed", dirProp="Direction")`: `Vector(round(speed·cos θ), round(speed·sin θ))`, округление `AwayFromZero`; `speed < 0` → `ArgumentOutOfRangeException`; переполнение → `OverflowException` (`checked`).
  Для `Angle(1)` (45°) и `speed=10` даёт `(7,7)`.
- `RegisterIoCDependencyContextCommand(depKey, commandKey, role)`: регистрирует `depKey`, который из `ICommandContext` берёт объект роли `role` и резолвит `commandKey` с этим объектом. `depKey != commandKey` обязательно.
- `Vector.operator +` с разной размерностью → `ArgumentException("Vectors must have the same dimensions")`.
- `RoslynSourceCompiler` в trunk кэширует статически по тексту; shooting и свежий серверный стек используют кэш экземпляра. `RegisterIoCDependencyGeneratedStrategies` делегирует общую GeneratedStrategyRegistration.

---
## 3.2 Movement — `feature/movement-fuel`

| | |
|---|---|
| Команды | `MoveCommand(IMoving)`: `Position += Velocity`; `CheckFuelCommand(IFuelCheckable)`: `Fuel < FuelConsumption` → `InvalidOperationException("Not enough fuel to move.")`; `BurnFuelCommand(IFuelBurnable)`: `Fuel -= FuelConsumption` |
| Интерфейсы | `IMoving{Vector Position{get;set;}; Vector Velocity{get;}}`, `IFuelCheckable{Fuel, FuelConsumption}`, `IFuelBurnable{Fuel{get;set;}, FuelConsumption}` |
| Действие | `Config/Movement.config`: Check → Move → Burn (порядок важен!) |
| Вызов | `Ioc.Resolve<Lib.ICommand>("Commands.Action", "Movement", ship)` |
| Свойства объекта | `Position`, `Velocity` **или** (`Speed`+`Direction` при `MovementDependencies.ComputedVelocity`), `Fuel`, `FuelConsumption` |
| Вычисляемая скорость | передать `[MovementDependencies.ComputedVelocity]` в `RegisterIoCDependencyGeneratedStrategies` и вызвать `new RegisterIoCDependencyComputedVelocity().Execute()` |
| Тесты | `MovementTests`, `MoveCommandTests`, `FuelCommandTests`, `MovementComputedVelocityTests` |

Если топлива не хватает — позиция и топливо не меняются (Check падает первым). Отсутствующее свойство даёт `KeyNotFoundException`; отсутствие отката в Macro означает, что последствия ошибки зависят от шага, на котором она возникла.

---
## 3.3 Rotation — `feature/rotation`

`RotateCommand(IRotating)`: `Direction += AngularVelocity` (оператор `Angle+`, по модулю 8). `IRotating{Angle Direction{get;set;}; Angle AngularVelocity{get;}}`.
Действие `Rotation` (`Config/Rotation.config` = `Commands.SpaceBattle_Lib_RotateCommand`). Тесты: `RotateTest`, `RotationActionTests`; тест-дублёры `TestDoubles/RotationCommands.cs`.

---
## 3.4 Refuel — два взаимоисключающих варианта

### A. `feature/refuel` (старая база `0e79e07`, удалена на GitHub в ходе аудита)
- `CheckRefuelSourceCommand(IRefuelCheckable)`: `FuelReserve < RefuelAmount` → `ExceptionFactory.InvalidOperation("Not enough fuel in reserve to refuel.")`.
- `RefuelCommand(IRefuelable)`: `FuelReserve -= RefuelAmount; Fuel += RefuelAmount`.
- `Refuel.config`: Check → Refuel. Один объект (`Commands.Action`, "Refuel", item) со свойствами `Fuel`, `FuelReserve`, `RefuelAmount`, `ExceptionFactory`.
- Тесты: `RefuelTests`, `RefuelCommandTests` (число тестов — в свежей таблице статуса).
- Эта ветка сохраняет исторические IVectorFactory/IAngleFactory, RegisterIoCDependencyValueFactories и Utility.Vector/Angle. В актуальном trunk этих фабрик значений нет; конструирование Vector/Angle прямое.

### B. `feature/tanker-refuel` (выбрана в экспериментальной интеграции)
- `RefuelCommand(IRefuelTarget target, IRefuelSource source)`: `amount = min(source.RefuelAmount, source.FuelReserve)`; `target.Fuel += amount; source.FuelReserve -= amount` (отдаёт остаток, а не падает).
- `IRefuelTarget{int Fuel{get;set;}}`, `IRefuelSource{int FuelReserve{get;set;}; int RefuelAmount{get;}}`.
- `RegisterIoCDependencyRefuelContextCommand(depKey, targetRole, sourceRole)` (роли должны отличаться).
- `Refuel.config` = одна строка `Context.Refuel`. Вызов: `Resolve<Lib.ICommand>("Commands.ContextAction", "Refuel", context)`, где `context = new CommandContext({Target: ship, Source: tanker})`.
- Регистрация в тесте: `RegisterIoCDependencyGeneratedStrategiesWithAdapters([asm], [typeof(IRefuelTarget), typeof(IRefuelSource)], [])` → `RegisterIoCDependencyRefuelContextCommand("Context.Refuel","Target","Source")`.

⚠ Имена файлов/классов `RefuelCommand`, `Refuel.config`, `RefuelTests` совпадают — ветки **нельзя слить вместе**.

---
## 3.5 Shooting — `feature/shooting` (автор: maxximben)

| | |
|---|---|
| Команда | `ShootCommand(IShooting shooting)` (primary-constructor) |
| Интерфейс | `IShooting{Position, Direction, ProjectileSpeed, ProjectileDamage, Ammo{get;set;}, ProjectileFactory, Storage, ExceptionFactory}` (последние три приходят через `ShootingDependencies.All`, а не из свойств объекта) |
| Фабрика | `ProjectileFactory(IGameItemFactory, Func<IGameItem,Vector> velocity)`; регистрация `RegisterIoCDependencyProjectileFactory` → ключ `Factories.Projectile`; скорость берётся из `Properties.ComputedVelocity` |
| Действие | `Config/Shooting.config` = `Commands.SpaceBattle_Lib_ShootCommand`; вызов `Commands.Action`, "Shooting", ship |
| Регистрации в тесте | `ExceptionFactory`, `GameItemFactory`, `ComputedVelocity`, `ProjectileFactory`, `GameItemStorage(storage)`, `CodeGeneration`, `GeneratedStrategies(asm, ShootingDependencies.All)`, `Macro`, `ActionComposition`, `ActionConfigurations` |
| Ошибки | `Ammo <= 0` → `InvalidOperationException("Not enough ammunition to shoot.")`; в `Create`: `ArgumentNullException`, `ArgumentOutOfRangeException` (speed/damage < 0), `ArgumentException` (`position.Dim != 2`) |
| Тесты | `ShootingTests`, `ProjectileFactoryTests` |

Замечания:
- Актуальный tip `3ff48cf` включает SOLID-рефакторинг и `CompilerCacheIsolationTests`, кэш Roslyn больше не статический в этой ветке.
- ProjectileFactory копирует Position через `new Vector(position.Coordinates)`, валидирует position/direction/speed/damage; созданный снаряд получает Id, Position, Direction, Speed, Damage, Destroyed, Velocity. **Kind не добавляется**: для снимков по схемам Kind нужна отдельная интеграция.
- Ammo читается перед созданием снаряда и уменьшается после успешного `storage.Add`. Ошибка фабрики/добавления не расходует боезапас; полного транзакционного отката произвольного setter нет.
- `IShooting.ExceptionFactory` пока остаётся в контракте и ShootingDependencies, хотя ShootCommand бросает InvalidOperationException напрямую.

---
## 3.6 Collisions — `feature/collisions`

Файлы: `Collisions/{CollisionFile,CollisionOffset,CollisionTree}.cs`, `Commands/CollisionCommand.cs`,
`Registration/RegisterIoCDependencyCollisions.cs`, `RegisterIoCDependencyCollisionContextCommand.cs`, `tools/collisions/*`.
Ошибки формата строки → `FormatException("Line N: expected 4 integers (dx dy dvx dvy), got '...'")`.
Несуществующий каталог → `DirectoryNotFoundException`; существующий пустой каталог просто не регистрирует деревьев (фабрика Commands.Collision регистрируется). Несуществующее дерево → `Exception` (IoC «not found»).
Дерево строится **лениво** (`Lazy`) и кэшируется — повторный `Resolve` возвращает тот же объект. Тесты: `CollisionFileTests`, `CollisionTreeTests`, `CollisionCommandTests`, `CollisionRegistrationTests`, `CollisionContextCommandTests`.

---
## 3.7 Damage — `feature/damage` (⊃ collisions)

`ApplyDamageCommand(IDamageTarget{Health{get;set;}}, IDamageSource{Damage})`, `DestroyIfDeadCommand(IDestructible{Health, Destroyed{get;set;}})`,
`RegisterIoCDependencyDamageContextCommand(depKey, targetRole, sourceRole)`. `Config/Hit.config` = `Context.ApplyDamage`, `Context.DestroyIfDead`.
Пример полной сборки столкновения в `DamageContextTests`: `Strike.config` = `Commands.Hit.Collision`, который регистрируется
`RegisterIoCDependencyCollisionContextCommand("Commands.Hit.Collision", "Collisions.Ship-Torpedo", "Target", "Source", "Hit")`.
Результат: попадание → `Hit` (урон + уничтожение).

---
## 3.8 Game — `feature/game` (и в составе game-runner/messages/server-management)

- `Game(id, IGameQueue, IGameScope, IQuantum) : IGame` — `Post(cmd)`, `HasCommands`, `Execute()`: войти в скоуп → `quantum.Start()` → выполнять команды, пока есть и квант не истёк (первая команда выполняется всегда).
- `GameQueue` — обычная `Queue<ICommand>` (не потокобезопасная: доступ только из потока игры).
- `TimeQuantum(TimeSpan duration, TimeProvider)`; `duration < 0` → `ArgumentOutOfRangeException`; `Start()` фиксирует `GetTimestamp()`; `IsOver` = `elapsed ≥ duration`.
- `Games.Create(id)`: `BattleContextFactory.Create(id)` → новый `IoC.Scope.Create` → внутри `scope.Enter()` регистрируется `RegisterIoCDependencyBattleContext` → `new Game(...)`. То есть **у каждой игры свой IoC-скоуп и свой `Storages.GameItem`**.
- `Games.Quantum` — `RegisterIoCDependencyGameQuantum(TimeSpan[, TimeProvider])`.
- `GameDependencies` растёт вместе со стеком. В актуальной истории game → game-runner → messages → server-management; прежний конфликт этих веток относился к старым tips.

---
## 3.9 Server thread — `feature/server-thread`

`ICommandQueue` (`BlockingCommandQueue`): `Add`, `AddFirst`, `Take` (блокирует), `TryTake`, `CompleteAdding`, `Consume()` (ленивый перебор до закрытия), `Count`; после `CompleteAdding` `Add` → `InvalidOperationException`. Сейчас есть `TryAdd` и capacity; Add/AddFirst используются для служебных команд и не ограничены capacity.
`IScheduler` (`QueueScheduler`): FIFO для «запланированных» команд (игры).
`ServerThread` — см. архитектуру §6. `ServerThreads.Create` → новый поток (очередь, цикл, планировщик, обработчик исключений, фабрика исключений).
`HardStopCommand`/`SoftStopCommand` валидируют `Environment.CurrentManagedThreadId == thread.ThreadId`.
Тесты: `ServerThreadTests`, `ServerThreadRegistrationTests`, `CommandCollectionsTests`; дублёры `SpyCommandQueue`, `RecordingExceptionHandler`, `StubServerThread`.

---
## 3.10 Routing — `feature/routing`

`IHashFunction.Hash(string) → ulong`; `ConsistentHashRing<TNode>(hash, replicas)`: `Add(id,node)` (дубль → `ArgumentException`), `Remove(id)→bool`, `Find(key)` (пустое кольцо → `InvalidOperationException("The ring has no nodes.")`), `Count`.
Позиции узла: `hash($"{id}#{replica}")`; при коллизии выигрывает меньший `id`. Поиск — `BinarySearch` с переходом по кругу.
`RegisterIoCDependencyRouting(replicas[, hash])` → `Routing.Ring`, `Routing.Thread(gameId)`. По умолчанию FnvHashFunction; Sha256HashFunction — доступная альтернатива. Тестовый дублёр `FakeHashFunction`. Тесты: `ConsistentHashRingTests`, `Sha256HashFunctionTests`, `FnvHashFunctionTests`, `RoutingRegistrationTests`.

---
## 3.11 Game runner — `feature/game-runner`

`GameRunner(IGame, IServerThread)` — `Post(cmd)`: кладёт в игру и (однократно, флаг `_scheduled`) планирует себя на потоке; `Execute()`: прогнать игру, затем (в `finally`) перепланировать, если в игре остались команды — так игра получает кванты по очереди с другими.
`PostToGameCommand(runner, command)`. Ключи `Games.Runner(game, thread)`, `Games.Post(runner, cmd)`. Тесты: `GameRunnerTests`, `GameRunnerRegistrationTests`.

---
## 3.12 Messages — `feature/messages`

Формат сообщения (JSON-объект): `{"gameId": "<non-empty string>", "command": "<non-empty string>", "args": <любой JSON, необязателен>}`.
Нарушения → `MessageFormatException : FormatException` (не JSON / не объект / нет строки `gameId`/`command`).
`MessageReceiver.Receive(text)` → parse → `GameRegistry.Find(gameId).Send(msg)`; неизвестная игра → `KeyNotFoundException("The game \"X\" is not running on this server.")`.
`IoCMessageInterpreter` резолвит `Messages.Command.<command>` (прикладные команды регистрируются отдельно; начиная с game-items есть встроенный CreateItem).
`GameRegistry` — `ConcurrentDictionary`, дубль `gameId` → `ArgumentException`.
Тесты: `JsonMessageParserTests`, `MessageReceiverTests`, `GameRegistryTests`, `MessagesRegistrationTests`.

---
## 3.13 Server — `feature/server-management`

`Server(ring, createThread, softStop, hardStop, threadCount, prefix="Thread")`; публичный IServer объединяет IServerLifecycle/IServerThreads/IServerAvailability: IsRunning, ThreadCount, EnsureRunning, Start, AddThread и async stop;
повторный `Start` / стоп после стопа → `InvalidOperationException`; `AddThread` после перехода в ClosedServerState → `InvalidOperationException("The server is stopping.")`.
`RegisterIoCDependencyServer(threadCount)` → `Server.Instance`, `Server.Start`, `Server.SoftStop`, `Server.HardStop` (три последних — **команды**: `Resolve<Lib.ICommand>(key).Execute()`).
Тесты: `ServerTests`, `ServerRegistrationTests`; актуальные tips переведены на async-ожидания. В свежем стеке сервер реализован через `Servers/States/*`, узкие интерфейсы и выделенный сервис удаления потока.

### Минимальный запуск сервера целиком (из `ServerRegistrationTests`)

```csharp
new InitCommand().Execute();
Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", Ioc.Resolve<object>("IoC.Scope.Create")).Execute();
new RegisterIoCDependencyExceptionFactory().Execute();
Register(ServerThreadDependencies.ExceptionHandler, _ => myHandler);          // IExceptionHandler — обязателен
new RegisterIoCDependencyBattleContextFactory().Execute();
new RegisterIoCDependencyGameQuantum(TimeSpan.FromHours(1)).Execute();
new RegisterIoCDependencyGame().Execute();
new RegisterIoCDependencyServerThread().Execute();
new RegisterIoCDependencyGameRunner().Execute();
new RegisterIoCDependencyRouting(16).Execute();                               // 16 виртуальных узлов
new RegisterIoCDependencyMessages().Execute();
Register(MessageDependencies.CommandKey("Echo"), args => /* ICommand */);     // команды протокола — свои
new RegisterIoCDependencyServer(4).Execute();                                 // 4 потока

Ioc.Resolve<Lib.ICommand>(ServerDependencies.Start).Execute();
Ioc.Resolve<IGameHost>(GameDependencies.Start, "Battle-1");                   // создать и зарегистрировать игру
Ioc.Resolve<IMessageReceiver>(MessageDependencies.Receiver)
   .Receive("""{"gameId":"Battle-1","command":"Echo","args":{"text":"hi"}}""");
Ioc.Resolve<Lib.ICommand>(ServerDependencies.SoftStop).Execute();
```

---
## 3.14 Снимок игры — `feature/game-snapshot`

Основные файлы: `Serialization/{SnapshotGameCommand,RestoreItemsCommand,ItemSerializer,IoCItemSchema,JsonValueCodec,CommandJsonSerializer,MessageCommandSerializer}.cs`, регистрации `GameSnapshot` и `ValueType<T>`.

`Games.Snapshot(game, Action<string> sink)` возвращает команду. При её исполнении в игровом скоупе сериализуются id игры, объекты storage и оставшаяся очередь. `Games.Restore(json)` создаёт игру, ставит первой RestoreItemsCommand, затем восстанавливает сериализуемые команды. До исполнения восстановленной игры storage ещё не обязан содержать восстановленные объекты.

Свойства объектов выбирает `Serialization.Schema.<Kind>`; Id/Kind служат идентичностью. Значение кодируется парой type/value через registry codecs: int, long, double, bool, string, angle, vector и зарегистрированные расширения. Неизвестные Kind/тип/команда требуют регистрации, а не молчаливого пропуска. Для дополнительных команд есть `ICommandSerializer`; один MessageCommandSerializer не сериализует любую игровую команду.

Тесты: `GameSnapshotTests`. Пределы: это JSON-состояние игры и очереди, не снимок задач, таймеров, сети или IoC-контейнера целиком.

## 3.15 Создание объектов — `feature/game-items`

`Items/{ItemCreator,CreateItemCommand,CreateItemMessageCommand}.cs`; `RegisterIoCDependencyGameItems`, `RegisterIoCDependencyItemKind`. `ItemCreator` создаёт объект по kind/id/properties, добавляет Id и Kind и помещает в storage текущего боя. Дубликат id отклоняется.

Протокол `CreateItem`: `args.id`, `args.kind`, `args.properties`; каждое значение properties кодировано через `{type,value}`. Например: `{"Fuel":{"type":"int","value":10}}`. `Items.Create(kind,id,properties)` возвращает IGameItem и **сразу** создаёт объект; командный путь — Messages.Command.CreateItem. Схема Kind определяет сохраняемые свойства и регистрируется отдельно. Тесты: `GameItemCreationTests`.

## 3.16 Снимки сервера — `feature/server-snapshot`

Новый `Runtime/*`: ManagedGame/Host, состояния host, GameRuntime, реестр, сервисы жизненного цикла и снимков. Интерфейсы отделяют реестр, starter, host lifecycle и runtime. `RegisterIoCDependencyManagedGames` переключает текущий скоуп на дочерний; добавляет сервисные ключи и заменяет стандартные Games.Create/Start/Registry для managed games.

`SnapshotAndStop(store)` ставит игровые барьеры, приостанавливает игры, сохраняет снимки работающих игр и делает soft stop. При сбое сохранения возвращает доступные игры в работу. `Restore(store)` проверяет дубликаты id и запускает восстановление. Есть `MemorySnapshotStore`; файловое хранилище приходит в server-host. Replacement store заменяет весь набор, поэтому уже перенесённые/удалённые игры не остаются в следующем снимке.

Тесты: `ServerSnapshotTests`, `ManagedGameValidationTests`, `RuntimeAbstractionTests`, `CompilerCacheIsolationTests`. Кэш Roslyn в этой линии изолирован по экземплярам.

## 3.17 Межсерверный перенос — `feature/game-transfer`

`GameTransferService` управляет межсерверным протоколом, `GameHostTransferService` — буфером/состоянием source host; `GameTransfer`/`IGameArrival` фиксируют или отменяют прибытие. Host остаётся доступным по прежнему id после переноса, но пересылает сообщения на destination.

Порядок: захват lifecycle без ожидания → BeginTransfer/pause → snapshot → Receive на target → Commit буфера → Moved/proxy. До commit сбой откатывает target и возобновляет source. Перенос на себя, занятый target и повторный id отклоняются. Переполнение transfer buffer даёт `ServerOverloadedException`. Политика буфера задана регистрацией; служебные барьеры не должны зависеть от свободного места в пользовательском inbox.

Тесты: `GameTransferTests`, `GameTransferFailureTests`. `IRemoteServer` — внедряемый контракт, готового HTTP-клиента транспорта в этой ветке нет.

## 3.18 Балансировка и удаление потоков

`feature/thread-rebalance`: `GameRebalanceService`, `IGameRebalanceService`, фасад `GameRuntime.Rebalance`. Добавить поток → обновить ring → найти игры с новым target → pause/snapshot/restore/relocate с сохранением идентичности host для клиентов. Ошибка переносимой игры вызывает CancelTransfer.

`feature/remove-thread`: `ThreadRemovalService`, `ServerThreadRemovalService`, `ServerThreadMember`, интерфейсы членства и управления потоками, классы состояний сервера. Удаляемый поток исключается из размещения до эвакуации; последний поток не удаляется; после удачной эвакуации — soft stop. Есть восстановление членства при ошибке.

Тесты: `ThreadRebalanceTests`, `ThreadRemovalTests`; в свежем стеке — также `ServerLifetimeAbstractionTests`. Простой вызов Server.AddThread и runtime.AddThread — разные уровни: перенос существующих игр делает runtime.

## 3.19 Приоритеты — `feature/priority-queue`

`Scheduling/PriorityCommandQueue.cs`: `IPrioritizedCommand.Priority`, wrapper `PrioritizedCommand`. Больший int означает более раннее выполнение; одинаковые priorities — FIFO по sequence под lock. `AddFirst` — отдельная срочная дорожка (новые urgent раньше предыдущих). Обычная команда имеет priority 0.

`TryAdd` учитывает capacity; Add/AddFirst нужны для служебной доставки и обходят ограничение. `RegisterIoCDependencyPriorityThreads(capacity)` заменяет стратегию ServerThreads.Create. Тесты: `PriorityQueueTests`.

## 3.20 Whitelist — `feature/command-whitelist`

`CommandWhitelist`, `WhitelistMessageInterpreter`, `WhitelistMessageReceiver`; `RegisterIoCDependencyCommandWhitelist(names)`. Множество имён сравнивается Ordinal; незаданные команды запрещены, пустые имена настроек отклоняются. Receiver проверяет до приёма, interpreter — перед исполнением, включая команды после restore/переноса.

Публичная точка композиции — `Messages.Decorate(Func<IMessageInterpreter,IMessageInterpreter>)`; receiver с валидацией — `Messages.ValidatedReceiver`. В HTTP-host используется AuthenticatedMessageReceiver со своим предварительным whitelist. Тесты: `CommandWhitelistTests`, `MessageDecoratorRegistrationTests`.

## 3.21 Ошибки — `feature/error-policy`

`ExceptionErrorPolicy`, `ErrorPolicyCommand`, `ErrorRecovery`, `IErrorAction` и отдельные Retry/Skip/StopGame/StopThread actions. Конфигурация: тип исключения → `(ErrorAction, Retries)`, отрицательные retries запрещены. Default — Skip; повтор исчерпан → Skip. `ILog`/MemoryLog записывают game id, exception, действие и попытку.

Игровые ошибки перехватываются через PolicyMessageInterpreter; State для паузы/остановки отделён от управления host. В server-host добавлены DeferredExceptionHandler, LoggingExceptionHandler и StopCurrentThreadErrorAction для ошибок непосредственно команд потока. Не путать эти два пути.

Тесты: `ErrorPolicyTests`, `ErrorRecoveryRegressionTests`; позже `ThreadErrorRecoveryTests`. Retry повторно выполняет действие без транзакции: безопасность повтора зависит от команды.

## 3.22 JWT — `feature/auth`

`PlayerIdentity(PlayerId,GameId,CanTransfer)`, `IPlayerTokenValidator`, `JwtPlayerTokenValidator`, `AuthenticatedMessage`, `AuthenticatedCommandSerializer`.

`RegisterIoCDependencyJwtAuthentication(issuer,audience,secret)` регистрирует Auth.Validator и Messages.Authenticate, вставляет authenticated serializer перед обычным message serializer. Допустим только подписанный HS256; проверяются issuer/audience/exp/signature, обязательны ровно один sub и один game; clock skew = 0; ключ минимум 32 UTF-8 байта. `permission=transfer` разрешает endpoint переноса.

Сообщение должно принадлежать игре из токена. PlayerId сохраняется в snapshot без повторного доверия входному JSON. Токены приложение не выдаёт. Тесты: `AuthenticationTests`.

## 3.23 HTTP — `feature/http-api`

Проект `SpaceBattle.Http`, ASP.NET Core minimal endpoints, public partial Program для тестового host. AuthenticatedMessageReceiver проверяет game/identity и whitelist, затем отдаёт сообщение runtime.

| Endpoint | Успех | Основные отказы |
|---|---|---|
| `POST /messages` с Bearer и JSON сообщения | 202, принято в обработку | 400 формат/whitelist; 401 JWT; 403 другая game; 404 игра; 503 перегрузка + Retry-After |
| `POST /games/{id}/transfer`, `{"destination":"name"}` | 204 после переноса | 401 JWT; 403 другая игра/нет transfer; 400 формат; 404 неизвестный destination; 503 занято/буфер; 502 IOException |

202 подтверждает admission, а не результат команды: права на объект/ошибки команды проверяются позже в игровом потоке. Иных endpoint управления играми/потоками или выдачи JWT нет. Тесты: `HttpApiTests`, `HttpTransferFailureTests`; GameApiFactory предоставляет тестовый host.

## 3.24 Запускаемое приложение — `feature/server-host`

`HostComposition.Configure` связывает options, ServerComposition, IServerApplication, логирование, HttpGameServices и hosted service. `ServerApplication.Start` запускает потоки, при необходимости восстанавливает snapshot, создаёт отсутствующие configured Games; ошибка старта завершает сервер. `Stop` идемпотентно возвращает одну stopping task и ожидает снимок/soft stop.

`ServerOptions`: ThreadCount, InboxCapacity, RingReplicas, QuantumMilliseconds, PriorityQueues, Games, AllowedCommands, ItemKinds, SnapshotOnStop, RestoreOnStart, SnapshotPath, JwtIssuer/Audience/Secret. В потомке player-authorization дополнены CommandTargets, Players, CommandAccessRules. Ключ JWT по умолчанию пуст — для запуска обязателен внешний параметр.

`FileSnapshotStore` реализует ISnapshotBatchStore: SaveAll заменяет весь набор через временный файл (отдельный ISnapshotReplacementStore реализован memory store). Таблица remote destinations по умолчанию пуста. Пример appsettings содержит demo/CreateItem, но не является полной конфигурацией player permissions в потомках.

Тесты: `ServerHostTests`, `FileSnapshotStoreTests`, `HostCompositionCoverageTests`, `ServerLifetimeAbstractionTests`, `ThreadErrorRecoveryTests`. Команды запуска и параметры — [07](07-playbook.md).

## 3.25 Права игрока и E2E

`feature/player-authorization`: `PlayerCommandRules`, `PlayerCommandPermissions`, `TargetItemResolver`, `PlayerItemPermissions`, `ScopedItemOwners`, `ExistingItemOwnerRule`, `NewItemOwnerRule`, `OwnerClaim`. Порядок проверки: команда разрешена игроку → target id из CommandTargets → объект разрешён игроку → access rules. Правила назначаются по команде, могут заменяться конфигурацией через `Auth.AccessRule.<name>`. Стандартные имена правил: existing-owner и new-owner. В Items игрока допускается "*" для любых id, но проверка владения остаётся.

Existing-owner сверяет Owner в storage игры, new-owner — Owner из properties CreateItem с authenticated PlayerId. Нет автоматического разрешения «любому игроку с валидным JWT». Проверка при каждом исполнении, без service locator в командах. Тесты: `PlayerAuthorizationTests`, `AccessRuleConfigurationTests`.

`test/server-e2e` добавляет `ServerEndToEndTests`: два игрока, создание объектов, переполнение inbox и 503, буферизация во время переноса, проксирование, snapshot/restore с Owner и продолжение работы, остановка. В сценарии прикладной AddFuel зарегистрирован тестом — это не встроенная HTTP-команда продукта. `ServerRuntimeArchitectureTests` проверяет отсутствие IoC/new в runtime-папках.

## 3.26 Ограничение нагрузки — `feature/bounded-load-routing`

Самостоятельный серверный стек на базе game-items, с версиями host/transfer/policies/auth, отличными от свежих tips. Классы: `IRouter<TNode>`, `ILoadMeter<TNode>`, `BoundedLoadRouter<TNode>`, `PlacementLoadMeter<TNode>`, `PlannedMoves<TNode>`, `PlanningRouter<TNode>`.

При размещении исключает текущий game key из подсчёта, рассчитывает потолок по числу игр и tolerance, проверяет до probes кандидатов кольца; если все заполнены — берёт минимальную нагрузку из проверенных. Планируемые перемещения считаются на target, чтобы серия решений не переполнила один поток. Регистрация заменяет Routing.Thread и маршрут runtime. Не измеряет CPU/задержки.

Тесты: `BoundedLoadRouterTests`, `BoundedLoadRoutingRegistrationTests`, `PlacementLoadMeterTests`, `PlannedMovesTests`. Для слияния со свежим E2E-стеком есть два конфликта, см. [01](01-branches.md); эта ветка не включает свежую player-authorization.

## 3.27 Тики и обновление мира

`feature/game-ticks`: GameTicks, GameTickFactory, Tick/Start/Stop/DeliverGameTickCommand, DelayedCommandScheduler. Factory получает TimeSpan step, worldStep, callback постановки команды в игру и maxCatchUpTicks (default 2). Step > 0; maxCatchUpTicks >= 0. Timer доставляет команду, а состояние мира изменяет игровой поток. Start/Stop защищены поколением; stale deliveries игнорируются. Ошибка worldStep или доставки останавливает расписание и сохраняет Failure.

Тесты: `GameTicksTests`, `GameTickRegistrationTests`, `DelayedCommandSchedulerTests`; детерминированные часы в TickTestClock. Ветка не содержит GameRuntime/HTTP — интеграция через callback.

`feature/world-update`: WorldStepCommand/Factory формируют frame с Battle и снимком Items, затем исполняют именованное действие WorldStep. ForEachWorldItemCommand применяет действие к выбранным объектам этого снимка; добавленные во время шага объекты не появляются в исходном списке задним числом.

JSON фаз: `{ "phases": [{ "name": "move", "execution": "items", "action": "Movement", "selector": "Selectors.Moving" }] }`. Для режима world selector запрещён, для items обязателен; неизвестные/повторные поля, имена фаз и режимы отклоняются. Дополнительные execution modes внедряются при регистрации loader. Имена IoC шагов имеют префикс и длину имени внешнего действия, чтобы не перезаписывать произвольные ключи.

Тесты: `WorldStepTests`, `WorldPhaseConfigurationTests`, `WorldStepTickIntegrationTests`. Готовых игровых конфигов физики и автоматического подключения к HTTP-host нет.

## 3.28 Дерево областей доступа — `feature/object-authorization`

`Authorization/{AccessAreaTree,ObjectAccessAreas,AccessGrantStore,InheritedGrantRule,AuthorizationPolicy,AuthorizationDecision}`, `AuthorizedActionFactory/Command` и `RegisterIoCDependencyAuthorizedActions`.

AccessAreaTree хранит пути, разрешает перенос поддерева и запрещает циклы/неизвестного родителя. ObjectAccessAreas связывает конкретный объект с областью. Grant имеет subject/action/area и наследуется потомками области. InheritedGrantRule возвращает Allow либо Abstain; общий policy разрешает только при наличии Allow и отсутствии Deny/некорректного решения. Без правил разрешения нет.

`Commands.AuthorizedAction(subject,action,item)` создаёт обёртку, которая проверяет актуальные права при Execute, затем создаёт и исполняет Commands.Action. Субъект должен поступить из доверенного источника; сама ветка JWT не проверяет. Ошибка прав не создаёт вложенное действие. Тесты: `ObjectAuthorizationTests`.

Это отдельный механизм от player-authorization, пока без автоматического HTTP adapter и общей конфигурации с Players/CommandTargets.
