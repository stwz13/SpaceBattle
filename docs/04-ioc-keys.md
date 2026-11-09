# 04. Каталог IoC-ключей

Срез 2026-11-09 по всем веткам, включая локальный исторический снимок refuel (в конце аудита уже удалена на GitHub). A–G — общая основа, H–L — расширения с привязкой к веткам. Наличие ключа-константы не означает, что соответствующая регистрация выполнена.

Формат: `ключ` (аргументы) → результат. «Кто регистрирует» — класс `RegisterIoCDependency*` (вызывается `.Execute()` **в текущем скоупе**).
Константы ключей лежат в `Dependencies/*Dependencies.cs` и `Utilities/DependencyNamespaces*.cs` — используйте константы, не строки, в новом коде.
`Lib.ICommand` = `SpaceBattle.Lib.ICommand`, `App.ICommand` = из `core.dll` (см. [02-architecture.md](02-architecture.md) §1).

Префиксы пространств (`DependencyNamespaces`): `Adapters`, `Commands`, `Properties`, `Contexts`, `Factories`, `Storages`, `Games`, `Messages`, `Routing`, `Server`, `ServerThreads`, `Items`, `Serialization`. Дополнительные ключи Auth/Errors/Snapshots/WorldStep/Configuration/Authorization заданы строками в своих Dependencies.

## A. Контейнер (scopes.dll, после `new InitCommand().Execute()`)
| Ключ | Аргументы | Результат |
|---|---|---|
| `IoC.Register` | `string key, Func<object[],object>` | `App.ICommand` |
| `IoC.Scope.Create` / `IoC.Scope.Create.Empty` | – | `object` scope |
| `IoC.Scope.Current` | – | `object` scope |
| `IoC.Scope.Current.Set` | `object scope` | `App.ICommand` |
| `IoC.Scope.Current.Clear` | – | `App.ICommand` |
| `IoC.Scope.Parent` | – | `object` scope |

## B. Базовые фабрики/хранилища/контексты
| Ключ | Аргументы | Результат | Кто регистрирует |
|---|---|---|---|
| `Factories.Exception` | – | `IExceptionFactory` | `RegisterIoCDependencyExceptionFactory` |
| `Factories.GameItem` | – | `IGameItemFactory` | `RegisterIoCDependencyGameItemFactory` |
| `Factories.BattleContext` | – | `IBattleContextFactory` | `RegisterIoCDependencyBattleContextFactory` |
| `Storages.GameItem` | – | `IGameItemStorage` (хранилище текущего боя) | `RegisterIoCDependencyGameItemStorage(storage)` (и внутри `...BattleContext`) |
| `Contexts.Battle` | – | `IBattleContext` | `RegisterIoCDependencyBattleContext(context)` |
| `Properties.ComputedVelocity` | `IGameItem` | `Vector` | `RegisterIoCDependencyComputedVelocity([key,speedProp,dirProp])` |
| `Factories.Projectile` | – | `IProjectileFactory` | `RegisterIoCDependencyProjectileFactory` (shooting) |

## C. Кодогенерация
| Ключ | Результат | Кто |
|---|---|---|
| `CodeGeneration.CommandFinder` | `ICommandFinder` | `RegisterIoCDependencyCodeGeneration` |
| `CodeGeneration.AdapterSourceBuilder` | `IAdapterSourceBuilder` | то же |
| `CodeGeneration.StrategySourceBuilder` | `IStrategySourceBuilder` | то же |
| `CodeGeneration.SourceCompiler` | `ISourceCompiler` | то же |

Сгенерированные (после `RegisterIoCDependencyGeneratedStrategies[WithAdapters]` / `...GeneratedAdapters`), `M(T)` = `T.FullName` с `.`/`+` → `_`:

| Ключ | Аргументы | Результат |
|---|---|---|
| `Adapters.M(IFoo)Adapter` | `IGameItem` | экземпляр адаптера, реализующий `IFoo` |
| `Commands.M(FooCommand)` | `IGameItem` (для команд с одним адаптируемым параметром) | `FooCommand`, обёрнутая вокруг адаптера |
| `Properties.M(IView).M(IDeclaring).Prop` | `IGameItem` | значение свойства (get) |
| `Properties.M(IView).M(IDeclaring).Prop.Set` | `IGameItem, object value` | `Lib.ICommand` (`SetPropertyCommand`) |

Примеры: `Commands.SpaceBattle_Lib_MoveCommand`, `Adapters.SpaceBattle_Lib_IRefuelTargetAdapter`, `Properties.SpaceBattle_Lib_IMoving.SpaceBattle_Lib_IMoving.Position`.
Хелперы: `GeneratedNames.CommandDependency(typeof(X))`, `GeneratedNames.AdapterDependency(typeof(I))`, `GeneratedNames.PropertyDependency(view, propertyInfo)`.

## D. Действия, макро, контекстные команды
| Ключ | Аргументы | Результат | Кто |
|---|---|---|---|
| `Commands.Macro` | `Lib.ICommand...` (params) | `MacroCommand` | `RegisterIoCDependencyMacroCommand` |
| `Commands.Action` | `string action, IGameItem item` | `Lib.ICommand` (макро по `<action>.Config`, шаги резолвятся с `item`) | `RegisterIoCDependencyActionComposition` |
| `Commands.ContextAction` | `string action, ICommandContext ctx` | `Lib.ICommand` (шаги резолвятся с `ctx`) | `RegisterIoCDependencyContextActionComposition` |
| `<Action>.Config` | – | `string[]` (имена ключей-шагов) | `RegisterIoCDependencyActionConfigurations(dir)` (по каждому `*.config`) |
| `Specs.<Action>` | – | `string[]` (то же) | то же |
| `<любой depKey>` вида `Context.Xxx` | `ICommandContext` | `Lib.ICommand` | `RegisterIoCDependencyContextCommand(depKey, commandKey, role)`; `…RefuelContextCommand`; `…DamageContextCommand`; `…CollisionContextCommand` |

Файлы действий в `SpaceBattle.Lib/Config/` (копируются в вывод): `Movement` (Check→Move→Burn), `Rotation`, `Shooting`, `Refuel` (A или B), `Hit` (ApplyDamage→DestroyIfDead).

## E. Столкновения
| Ключ | Аргументы | Результат |
|---|---|---|
| `Collisions.<path>` | – | `CollisionTree` (лениво). `<path>` = путь к `*.collisions` относительно каталога таблиц без расширения, разделители → `.`, напр. `Collisions.Ship-Ship.0-0` |
| `Commands.Collision` | `string treeKey, IGameItem a, IGameItem b, Lib.ICommand onCollision` | `CollisionCommand` |

Кто: `RegisterIoCDependencyCollisions(directory)`.

## F. Игра, сообщения, маршрутизация, сервер
| Ключ | Аргументы | Результат | Кто |
|---|---|---|---|
| `Games.Quantum` | – | `IQuantum` | `RegisterIoCDependencyGameQuantum(duration[, TimeProvider])` |
| `Games.Create` | `string id` | `IGame` (свой скоуп и бой) | `RegisterIoCDependencyGame` (нужны `Factories.BattleContext`, `Games.Quantum`) |
| `Games.Runner` | `IGame, IServerThread` | `IGameRunner` | `RegisterIoCDependencyGameRunner` |
| `Games.Post` | `IGameRunner, Lib.ICommand` | `PostToGameCommand` | то же |
| `Games.Registry` | – | `IGameRegistry` | `RegisterIoCDependencyMessages` |
| `Games.Start` | `string gameId` | `IGameHost` (создаёт игру, **регистрирует** в реестре) | то же |
| `Messages.Parser` | – | `IMessageParser` | то же |
| `Messages.Receiver` | – | `IMessageReceiver` | то же |
| `Messages.Command.<name>` | `IMessage` | `Lib.ICommand` | приложение/тест; game-items дополнительно регистрирует CreateItem |
| `Routing.Ring` | – | `IConsistentHashRing<IServerThread>` | `RegisterIoCDependencyRouting(replicas[, hash])` |
| `Routing.Thread` | `string gameId` | `IServerThread` | то же |
| `ServerThreads.Create` | – | `IServerThread` (не запущен) | `RegisterIoCDependencyServerThread([capacity])`; PriorityThreads заменяет создание |
| `ServerThreads.ExceptionHandler` | – | `IExceptionHandler` | приложение/тест в базовом стеке; ServerFoundation + ThreadErrorHandling в server-host |
| `Commands.HardStop` / `Commands.SoftStop` | `IServerThread` | `Lib.ICommand` | `RegisterIoCDependencyServerThread([capacity])`; PriorityThreads заменяет создание |
| `Server.Instance` | – | `IServer` | `RegisterIoCDependencyServer(threadCount)` |
| `Server.Start` / `Server.SoftStop` / `Server.HardStop` | – | `Lib.ICommand` | то же |

Строки-ключи для `Games.*`: `GameDependencies.{Create,Quantum,Runner,Post,Registry,Start}`; для сервера `ServerDependencies.{Instance,Start,SoftStop,HardStop}`;
сообщения `MessageDependencies.{Receiver,Parser,CommandKey(name)}`; маршрутизация `RoutingDependencies.{Ring,Thread}`; поток `ServerThreadDependencies.{Create,ExceptionHandler,HardStop,SoftStop}`.

## G. Порядок регистрации (зависимости между регистрациями)
1. `InitCommand` → новый скоуп (`IoC.Scope.Create` + `Current.Set`).
2. Базовое: `ExceptionFactory`, `GameItemFactory`, `ComputedVelocity`, `GameItemStorage`/`BattleContext…`.
3. `CodeGeneration` → затем `GeneratedStrategies…` (нужны `CodeGeneration.*`, а также все ключи, на которые ссылаются `Dependency`-перенаправления — они резолвятся лениво в момент `get`).
4. `Macro` → `ActionComposition`/`ContextActionComposition` → `ActionConfigurations(dir)`.
5. Контекстные команды (`Context.*`) — после `ContextActionComposition`; ключи шагов должны существовать к моменту `Resolve`, не к моменту регистрации.
6. Сервер: `ExceptionFactory`, `ExceptionHandler`, `BattleContextFactory`, `GameQuantum`, `Game`, `ServerThread`, `GameRunner`, `Routing`, `Messages`, `Server`.

⚠ Регистрации **скоупозависимы**: стратегия, зарегистрированная в скоупе игры, не видна из другого скоупа; `RegisterIoCDependencyServer` запоминает **текущий** скоуп (`IoC.Scope.Current`) и создаёт потоки/стоп-команды внутри него.


## H. Сериализация и создание объектов

Начиная с `feature/game-snapshot`, затем `feature/game-items`.

| Ключ | Аргументы → результат | Регистрация/замечание |
|---|---|---|
| `Serialization.Codec` | нет → IValueCodec | GameSnapshot; JsonValueCodec |
| `Serialization.CodecRegistry` | нет → IValueCodecRegistry | тот же codec, для ValueType<T> |
| `Serialization.CommandSerializers` | нет → List<ICommandSerializer> | расширяемый список, порядок важен |
| `Serialization.Schema.<kind>` | нет → IReadOnlyCollection<string> | ItemKind(kind, properties); имена сохраняемых свойств |
| `Games.Snapshot` | IGame, Action<string> → ICommand | SnapshotGameCommand; исполнять в скоупе игры |
| `Games.Restore` | string JSON → IGame | создаёт игру и ставит восстановление в очередь; в managed runtime возвращает IManagedGame |
| `Items.Create` | string kind, string id, IEnumerable<KeyValuePair<string,object>> → IGameItem | GameItems; **сразу создаёт и добавляет в storage**, это не ICommand |
| `Messages.Command.CreateItem` | IMessage → ICommand | GameItems; исполнение декодирует properties и создаёт объект |

`RegisterIoCDependencyValueType<T>(name, encode, decode)` дополняет registry, отдельного ключа на каждый type не создаёт. Встроенные имена: int, long, double, bool, string, angle, vector. Не путать константы `IdProperty`, `KindProperty`, `CreateCommandKey` со самостоятельными IoC-ключами.

## I. Managed runtime, снимки, топология

Начиная с `server-snapshot` и последовательно добавляемых веток. Контракты ниже — по свежему E2E-стеку, в bounded-load старые реализации могут быть иными.

| Ключ | Аргументы → результат | Кто |
|---|---|---|
| `Server.Games` | нет → GameRuntime / IGameRuntime | ManagedGames |
| `Server.GameRegistry` | нет → IManagedGameRegistry | ManagedGames |
| `Server.LifecycleGate` | нет → SemaphoreSlim | ManagedGames, общая синхронизация управляющих операций |
| `Server.RestoreGame` | нет → Func<string,IManagedGame> | ManagedGames; scoped restore delegate |
| `Server.GameHostFactory` | нет → Func<IManagedGame,IServerThread?,IManagedGameHost> | ManagedGames |
| `Games.Create` | string id → IManagedGame | переопределение ManagedGames |
| `Games.Registry` | нет → GameRuntime (IGameRegistry) | переопределение ManagedGames |
| `Games.Start` | string id ИЛИ IManagedGame → IManagedGameHost | переопределение ManagedGames |
| `Games.StartRestored` | IManagedGame → IManagedGameHost | ManagedGames |
| `Server.SnapshotAndStop` | ISnapshotStore → Task<IReadOnlyDictionary<string,string>> | ManagedGames; операция начинается при Resolve |
| `Server.Restore` | ISnapshotStore → Task | ManagedGames; ожидать возвращённую задачу |
| `Snapshots.Memory` | нет → новый MemorySnapshotStore | ManagedGames; это не singleton store |
| `Commands.Server.SnapshotAndStop` | ISnapshotStore → ICommand | ManagedGames; командная оболочка |
| `Games.TransferBufferCapacity` | нет → int | GameTransfer(bufferCapacity), default 10000; host передаёт InboxCapacity |
| `Games.Transfer` | string id, IRemoteServer → Task | GameTransfer |
| `Server.Remote` | нет → IRemoteServer (runtime) | GameTransfer |
| `Server.AddThread` | нет → Task<IServerThread> | Rebalancing; с перемещением игр |
| `Server.Rebalance` | нет → Task | Rebalancing |
| `Server.RemoveThread` | string threadId → Task | ThreadRemoval |

Server.Start/SoftStop/HardStop из базового каталога возвращают **ICommand**, тогда как управляющие ключи с Task в таблице начинают операцию при Resolve. Не вызывать Execute на Task и не терять await.

## J. Конвейер, политика ошибок, JWT и права игроков

| Ключ | Аргументы → результат | Кто |
|---|---|---|
| `Messages.Interpreter` | нет → IMessageInterpreter | ManagedGames; актуальная цепочка |
| `Messages.Decorate` | Func<IMessageInterpreter,IMessageInterpreter> → IMessageInterpreter | ManagedGames; изменяет цепочку и возвращает новый interpreter |
| `Messages.Whitelist` | нет → ICommandWhitelist | CommandWhitelist |
| `Messages.ValidatedReceiver` | нет → IMessageReceiver | CommandWhitelist; wrapper с предварительной проверкой |
| `Errors.Policy` | нет → IErrorPolicy | ErrorPolicy(rules, log) |
| `Errors.Log` | нет → ILog | ErrorPolicy |
| `Auth.Validator` | нет → IPlayerTokenValidator | JwtAuthentication(issuer,audience,secret) |
| `Messages.Authenticate` | IMessage, PlayerIdentity → IMessage/IAuthenticatedMessage | JwtAuthentication; проверяет gameId |
| `Auth.CommandRules` | нет → IPlayerCommandRules | PlayerAuthorization |
| `Auth.AccessRule.existing-owner` | нет → IAccessRule | AccessRules |
| `Auth.AccessRule.new-owner` | нет → IAccessRule | AccessRules |

`Auth.AccessRule.<name>` — расширяемое семейство. CommandTargets/Players/CommandAccessRules сами по себе не ключи контейнера, они параметры регистраций. HttpGameServices, IServerApplication и ServerOptions регистрируются также в DI ASP.NET, это другой контейнер, не `App.Ioc`.

`RegisterIoCDependencyPriorityThreads(capacity)` переопределяет ServerThreads.Create и добавляет:

| Ключ | Аргументы → результат |
|---|---|
| `Queues.Priority` | нет → новый PriorityCommandQueue с настроенной вместимостью |
| `Commands.Priority` | ICommand, int priority → PrioritizedCommand |

`RegisterIoCDependencyBoundedLoadRouting(tolerance,probes)` в отдельной ветке переопределяет Routing.Thread и делегат runtime.Route, новых именованных IoC-ключей не добавляет.

## K. Тики, шаг мира и конфигурация фаз

`feature/game-ticks` / `feature/world-update` независимы от managed runtime.

| Ключ | Аргументы → результат | Кто |
|---|---|---|
| `Factories.GameTicks` | нет → IGameTickFactory | GameTicks(scheduler) |
| `GameTicks.Create` | TimeSpan step, ICommand worldStep, Action<ICommand> post [, int maxCatchUpTicks] → IGameTicks | default catch-up 2 |
| `Commands.GameTicks.Start` / `Commands.GameTicks.Stop` | IGameTickControl → ICommand | GameTicks |
| `Factories.WorldStep` | нет → IWorldStepFactory | WorldStep([action]) |
| `Commands.WorldStep` | нет → ICommand | WorldStep |
| `WorldStep.Frame` | нет → IGameItem | WorldStep; свойства Battle и Items |
| `Configuration.WorldPhases.Loader` | нет → IWorldPhaseConfigurationLoader | WorldPhaseConfigurationLoader([additionalModes]) |
| `WorldPhaseConfiguration.Modes` | нет → IReadOnlyDictionary<WorldPhaseExecution,WorldPhaseMode> | внутренняя зависимость loader/registration |
| `<action>.Config` (default WorldStep.Config) | нет → string[] | ConfiguredWorldPhases(path,action) |
| `WorldStep.Phases.<length>.<action>.<phase>` | IGameItem frame → ICommand | ConfiguredWorldPhases; длина action включена в ключ |
| произвольный depKey фазы | IGameItem frame → ICommand | WorldItemsAction(depKey,action[,includes]) |
| ключ selector из JSON | нет → Func<IGameItem,bool> | приложение/тест |

Для конфигурируемого шага нужны также GameItemFactory, BattleContext в скоупе, Macro/ActionComposition, действия фаз и селекторы. Одной регистрации tick scheduler для обновления мира недостаточно.

## L. Объектная авторизация

Только `feature/object-authorization` и её экспериментальные интеграции:

| Ключ | Аргументы → результат | Кто |
|---|---|---|
| `Authorization.Policy` | нет → IAuthorizationPolicy | AuthorizedActions(policy) |
| `Factories.AuthorizedAction` | нет → IAuthorizedActionFactory | та же регистрация |
| `Commands.AuthorizedAction` | string trustedSubject, string action, IGameItem → ICommand | та же; проверка прав при Execute |

Дерево областей, привязки объектов и grants передаются policy через конструкторы, отдельных глобальных IoC-ключей для них здесь нет. Семейства Authorization.* и Auth.* не взаимозаменяемы.

## M. Порядок полной серверной композиции

В свежем `ServerComposition.Create(options, snapshots?, configureCommands?, log?)`:

1. InitCommand, отдельный серверный скоуп, валидация численных options.
2. ServerFoundation: фабрики, quantum/game, deferred handler, threads (обычные или priority), routing, runner, messages, server.
3. ServerRuntime: snapshot codecs, game-items, схемы Kind, managed games (дочерний скоуп), transfer, rebalance, removal.
4. ServerSecurity: JWT, whitelist, стандартные access rules.
5. configureCommands — прикладные команды в текущем скоупе.
6. ServerPolicies: player authorization (в player-authorization/E2E), error policy, binding thread exception handler.
7. Создание ServerApplication со scoped callbacks; восстановление прежнего скоупа в finally.

Регистрация ManagedGames должна предшествовать декораторам; snapshot — JWT serializer; transfer — rebalancing; rebalancing — removal. Сервисы обращения к игровому storage должны исполняться в скоупе игры, а не в HTTP-потоке.

## N. Исторические ключи только feature/refuel

`RegisterIoCDependencyValueFactories` сохранился в старой refuel и отсутствует в актуальном trunk. Не переносить это API на современные ветки, где Vector/Angle конструируются напрямую.

| Ключ | Аргументы → результат |
|---|---|
| `Factories.Vector` | нет → IVectorFactory (VectorFactory), Create/Add |
| `Factories.Angle` | нет → IAngleFactory (AngleFactory), Create/Add |
| `Utility.Vector` | int[] coordinates → Vector через фабрику |
| `Utility.Angle` | int numerator → Angle через фабрику |

Сигнатуры и валидация старой регистрации отличаются от новых: часть аргументов приводится по индексу без предварительной проверки. Также в этой ветке GeneratedStrategies ещё содержит собственную полную реализацию регистрации, а не делегирует GeneratedStrategyRegistration.
