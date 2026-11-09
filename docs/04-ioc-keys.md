# 04. Каталог IoC-ключей

Формат: `ключ` (аргументы) → результат. «Кто регистрирует» — класс `RegisterIoCDependency*`, вызывается `.Execute()` в **текущем скоупе**. В новом коде используйте константы из `Dependencies/*Dependencies.cs`, а не строки.
`Lib.ICommand` = `SpaceBattle.Lib.ICommand`, `App.ICommand` — из `core.dll` ([02-architecture.md](02-architecture.md) §1).

Префиксы (`DependencyNamespaces`): `Adapters`, `Commands`, `Properties`, `Contexts`, `Factories`, `Storages`, `Games`, `Items`, `Messages`, `Routing`, `Serialization`, `Server`, `ServerThreads`.

## A. Контейнер (`scopes.dll`, после `new InitCommand().Execute()`)

| Ключ | Аргументы | Результат |
|---|---|---|
| `IoC.Register` | `string key, Func<object[],object>` | `App.ICommand` |
| `IoC.Scope.Create` / `IoC.Scope.Create.Empty` | – | скоуп |
| `IoC.Scope.Current` | – | текущий скоуп |
| `IoC.Scope.Current.Set` | `scope` | `App.ICommand` |
| `IoC.Scope.Current.Clear` | – | `App.ICommand` |
| `IoC.Scope.Parent` | – | родительский скоуп |

## B. Базовые фабрики, хранилища, контексты

| Ключ | Аргументы | Результат | Кто регистрирует |
|---|---|---|---|
| `Factories.Exception` | – | `IExceptionFactory` | `…ExceptionFactory` |
| `Factories.GameItem` | – | `IGameItemFactory` | `…GameItemFactory` |
| `Factories.BattleContext` | – | `IBattleContextFactory` | `…BattleContextFactory` |
| `Storages.GameItem` | – | `IGameItemStorage` (текущей игры) | `…GameItemStorage(storage)`, `…BattleContext` |
| `Contexts.Battle` | – | `IBattleContext` | `…BattleContext(context)` |
| `Properties.ComputedVelocity` | `IGameItem` | `Vector` | `…ComputedVelocity` |
| `Factories.Projectile` | – | `IProjectileFactory` | `…ProjectileFactory` (стрельба) |

## C. Кодогенерация

| Ключ | Результат | Кто |
|---|---|---|
| `CodeGeneration.CommandFinder` | `ICommandFinder` | `…CodeGeneration` |
| `CodeGeneration.AdapterSourceBuilder` | `IAdapterSourceBuilder` | то же |
| `CodeGeneration.StrategySourceBuilder` | `IStrategySourceBuilder` | то же |
| `CodeGeneration.SourceCompiler` | `ISourceCompiler` | то же |

Сгенерированные ключи (`M(T)` = `FullName` с `.`/`+` → `_`):

| Ключ | Аргументы | Результат |
|---|---|---|
| `Adapters.M(IFoo)Adapter` | `IGameItem` | адаптер, реализующий `IFoo` |
| `Commands.M(FooCommand)` | `IGameItem` | `FooCommand` вокруг адаптера |
| `Properties.M(IView).M(IDeclaring).Prop` | `IGameItem` | значение свойства |
| `Properties.M(IView).M(IDeclaring).Prop.Set` | `IGameItem, object` | `Lib.ICommand` (`SetPropertyCommand`) |

Хелперы: `GeneratedNames.CommandDependency(typeof(X))`, `AdapterDependency(typeof(I))`, `PropertyDependency(view, propertyInfo)`.

## D. Действия, макро, контекстные команды

| Ключ | Аргументы | Результат | Кто |
|---|---|---|---|
| `Commands.Macro` | `Lib.ICommand...` | `MacroCommand` | `…MacroCommand` |
| `Commands.Action` | `string action, IGameItem item` | макро по `<action>.Config` | `…ActionComposition` |
| `Commands.ContextAction` | `string action, ICommandContext ctx` | макро с контекстом | `…ContextActionComposition` |
| `<Action>.Config`, `Specs.<Action>` | – | `string[]` шагов | `…ActionConfigurations(dir)` |
| `Context.Xxx` (любой `depKey`) | `ICommandContext` | `Lib.ICommand` | `…ContextCommand`, `…RefuelContextCommand`, `…DamageContextCommand`, `…CollisionContextCommand` |

Файлы действий (`Config/`): `Movement`, `Rotation`, `Shooting`, `Refuel`, `Hit` (последний — в ветке damage).

## E. Столкновения

| Ключ | Аргументы | Результат |
|---|---|---|
| `Collisions.<путь>` | – | `CollisionTree` (лениво); путь — к `*.collisions` без расширения, разделители → `.` (например `Collisions.Ship-Ship.0-0`) |
| `Commands.Collision` | `string treeKey, IGameItem a, IGameItem b, Lib.ICommand onCollision` | `CollisionCommand` |

Кто: `…Collisions(directory)`.

## F. Игра, сообщения, маршрутизация, сервер

| Ключ | Аргументы | Результат | Кто |
|---|---|---|---|
| `Games.Quantum` | – | `IQuantum` | `…GameQuantum(duration[, TimeProvider])` |
| `Games.Create` | `string id` | `IManagedGame` (своя игра и скоуп) | `…ManagedGames` (на базе `…Game`) |
| `Games.Runner` | `IGame, IServerThread` | `IGameRunner` | `…GameRunner` |
| `Games.Post` | `IGameRunner, Lib.ICommand` | `PostToGameCommand` | то же |
| `Games.Registry` | – | `IGameRegistry` | `…Messages` |
| `Games.Start` | `string gameId` | `IGameHost` | то же |
| `Games.StartRestored` | `IManagedGame` | управляемый хост восстановленной игры | `…ManagedGames` |
| `Messages.Parser`, `Messages.Receiver` | – | `IMessageParser`, `IMessageReceiver` | `…Messages` |
| `Messages.Command.<имя>` | `IMessage` | `Lib.ICommand` | **приложение** (протокол команд); `CreateItem` регистрирует `…GameItems` |
| `Messages.Interpreter` | – | `IMessageInterpreter` (с учётом декораторов) | `…ManagedGames` |
| `Messages.Decorate` | `Func<IMessageInterpreter, IMessageInterpreter>` | новый интерпретатор | то же |
| `Messages.Whitelist` | – | `ICommandWhitelist` | `…CommandWhitelist(names)` |
| `Messages.Authenticate` | `IMessage, PlayerIdentity` | `IAuthenticatedMessage` | `…JwtAuthentication` |
| `Routing.Ring` | – | `IConsistentHashRing<IServerThread>` | `…Routing(replicas[, hash])` |
| `Routing.Thread` | `string gameId` | `IServerThread` | `…Routing`; при ограничении нагрузки заменяется `…BoundedLoadRouting` |
| `ServerThreads.Create` | – | `IServerThread` (не запущен) | `…ServerThread` или `…PriorityThreads(capacity)` |
| `ServerThreads.ExceptionHandler` | – | `IExceptionHandler` | `…ServerFoundation` (отложенный), затем `…ThreadErrorHandling` |
| `Commands.HardStop`, `Commands.SoftStop` | `IServerThread` | `Lib.ICommand` | `…ServerThread` |
| `Server.Instance` | – | `IServer` | `…Server(threadCount)` |
| `Server.Start`, `Server.SoftStop`, `Server.HardStop` | – | `Lib.ICommand` | то же |

## G. Предметы и снимки

| Ключ | Аргументы | Результат | Кто |
|---|---|---|---|
| `Items.Create` | `string kind, string id, IEnumerable<KeyValuePair<string,object>>` | `IGameItem` (создан и положен в хранилище) | `…GameItems` |
| `Serialization.Codec`, `Serialization.CodecRegistry` | – | `IValueCodec` / реестр типов | `…GameSnapshot` |
| `Serialization.CommandSerializers` | – | список `ICommandSerializer` | то же |
| `Serialization.Schema.<kind>` | – | `string[]` свойств вида | `…ItemKind(kind, свойства)` |
| `Games.Snapshot` | `IGame, Action<string>` | `Lib.ICommand` (результат — в приёмник) | `…GameSnapshot` |
| `Games.Restore` | `string json` | `IGame` | то же |

## H. Управляемые игры, перенос, ребаланс

| Ключ | Что это | Кто |
|---|---|---|
| `Server.Games` | `GameRuntime` — фасад управления играми | `…ManagedGames` |
| `Server.GameRegistry`, `Server.LifecycleGate`, `Server.RestoreGame`, `Server.GameHostFactory` | внутренние сервисы runtime | то же |
| `Server.SnapshotAndStop`, `Server.Restore`, `Commands.Server.SnapshotAndStop` | снимок всех игр с остановкой; восстановление | то же |
| `Snapshots.Memory` | хранилище снимков в памяти | то же |
| `Games.TransferBufferCapacity` | `int` — размер буфера переноса | `…GameTransfer(capacity)` |
| `Games.Transfer`, `Server.Remote` | перенос игры; представление этого сервера как `IRemoteServer` | `…GameTransfer` |
| `Server.AddThread`, `Server.Rebalance` | добавить поток с ребалансом; только ребаланс | `…Rebalancing` |
| `Server.RemoveThread` | `string threadId` — удалить поток | `…ThreadRemoval` |

## I. Ошибки и безопасность

| Ключ | Результат | Кто |
|---|---|---|
| `Errors.Policy` | `IErrorPolicy` | `…ErrorPolicy(rules, log)` |
| `Errors.Log` | `ILog` | то же |
| `Auth.Validator` | `IPlayerTokenValidator` | `…JwtAuthentication(issuer, audience, secret)` |
| `Auth.AccessRule.<имя>` | `IAccessRule` (`existing-owner`, `new-owner`, свои) | `…AccessRules` |
| `Auth.CommandRules` | `IPlayerCommandRules` | `…PlayerAuthorization` |
| `Authorization.Policy`, `Factories.AuthorizedAction`, `Commands.AuthorizedAction` | объектная авторизация (ствол) | `…AuthorizedActions(policy)` |

## J. Тики и шаг мира (ствол, `world-update`)

| Ключ | Аргументы | Результат |
|---|---|---|
| `Factories.GameTicks` | – | `IGameTickFactory` |
| `GameTicks.Create` | `TimeSpan step, ICommand worldStep, Action<ICommand> post[, int maxCatchUp]` | `IGameTicks` |
| `Commands.GameTicks.Start`, `Commands.GameTicks.Stop` | `IGameTickControl` | `Lib.ICommand` |
| `Factories.WorldStep`, `Commands.WorldStep` | – | фабрика и команда шага мира |
| `WorldStep.Frame` | – | предмет-кадр (`Battle`, `Items`) |
| `WorldStep.Phases.<длина>.<action>.<имя>` | – | команда фазы |
| `Configuration.WorldPhases.Loader` | – | `IWorldPhaseConfigurationLoader` |

## K. Порядок регистрации

Для сервера всё делает `ServerComposition.Create`:
1. `InitCommand` → новый скоуп.
2. **Foundation**: фабрика исключений, фабрика контекста боя, фабрика предметов, квант, игра, отложенный обработчик исключений потока, потоки (приоритетные или обычные), маршрутизация, раннер игры, сообщения, сервер.
3. **Runtime**: снимки (`GameSnapshot`), предметы (`GameItems`), виды предметов из `ItemKinds`, управляемые игры, перенос, ребаланс, ограничение нагрузки (если задан `LoadTolerance`), удаление потока.
4. **Security**: JWT, белый список, правила доступа.
5. `configureCommands` — регистрация `Messages.Command.<имя>` приложением.
6. **Policies**: права игроков (после правил доступа, поэтому они уже есть), политика ошибок, обработка ошибок потока.

Для игровых действий: `CodeGeneration` → `GeneratedStrategies…` → `Macro` → `ActionComposition`/`ContextActionComposition` → `ActionConfigurations(dir)` → `Context.*`-команды. Ключи шагов должны существовать к моменту `Resolve`, а не к моменту регистрации.

⚠ `RegisterIoCDependencyServer` и `ManagedGames` запоминают **текущий** скоуп на момент `Execute()`: потоки, игры и стоп-команды создаются внутри него.
