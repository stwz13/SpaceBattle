# 02. Архитектура SpaceBattle

Срез 2026-11-09 по 37 снимкам: 36 актуальных веток и историческая refuel, удалённая на GitHub в ходе аудита. Разделы 1–8 описывают общую основу и игровые ветки; разделы 9–12 — современный серверный стек и независимые расширения. Состав конкретной ветки указан в [01](01-branches.md). Нельзя считать весь описанный код одной опубликованной сборкой.
Язык/платформа: C# 14, **.NET 10** (`net10.0`), `Nullable=enable`, `ImplicitUsings=enable`. Тесты: xUnit 2.9.3 + Moq 4.20.72 + coverlet.

## 1. Слои и сборки

```
SpaceBattle.slnx
├── Assemblies/core.dll      — App.Ioc, App.ICommand, UpdateIocResolveDependencyStrategyCommand   (даны, исходников нет)
├── Assemblies/scopes.dll    — App.Scopes.InitCommand, DependencyResolver, IDependencyResolver   (даны, исходников нет)
├── SpaceBattle.Lib          — вся логика (+ Config/*.config копируются в вывод)
│     NuGet: Microsoft.CodeAnalysis.CSharp 4.14.0  (Roslyn — компиляция сгенерированных адаптеров)
├── SpaceBattle.Http         — ASP.NET Core, Program/ApiEndpoints; начиная с feature/http-api
└── SpaceBattle.Tests        — xUnit; ссылается на Lib и на обе DLL; Roslyn подтягивается транзитивно через Lib (нужен архитектурным тестам)
```

`core.dll`/`scopes.dll` — учебные библиотеки преподавателя; их **нельзя менять**, API:

```csharp
// core.dll
namespace App {
  public interface ICommand { void Execute(); }                         // ≠ SpaceBattle.Lib.ICommand!
  public static class Ioc { public static T Resolve<T>(string dependency, params object[] args); }
}
// scopes.dll
new App.Scopes.InitCommand().Execute();    // инициализирует корневой скоуп и ключи IoC.*
```

### ⚠ Две разные `ICommand`
В проекте **два** интерфейса `ICommand` с одним методом `Execute()`:
- `App.ICommand` — из `core.dll`; им пользуется только сам IoC (`IoC.Register`, `IoC.Scope.Current.Set` возвращают его).
- `SpaceBattle.Lib.ICommand` — доменный интерфейс; **все команды игры** реализуют его.

В Lib-коде `ICommand` без префикса = `SpaceBattle.Lib.ICommand` (побеждает тип из своего namespace, даже при `using App;`).
`App.ICommand` пишется **всегда с префиксом**: `Ioc.Resolve<App.ICommand>("IoC.Register", ...)`.
В тестах (namespace `SpaceBattle.Tests`) доменную пишут `Lib.ICommand`. Не перепутайте — это самая частая ошибка.

## 2. IoC и скоупы

Единая точка получения зависимостей — `Ioc.Resolve<T>(key, args...)`. Стратегия по ключу — `Func<object[], object>`.

Ключи самого контейнера (строки найдены в `scopes.dll`; семантика выведена из использования, исходников нет):

| Ключ | Аргументы | Результат |
|---|---|---|
| `IoC.Register` | `key`, `Func<object[],object>` | `App.ICommand`; `.Execute()` регистрирует стратегию **в текущем скоупе** |
| `IoC.Scope.Create` | – | `object` — новый (дочерний) скоуп |
| `IoC.Scope.Create.Empty` | – | `object` — скоуп без родителя (по названию) |
| `IoC.Scope.Current` | – | `object` — текущий скоуп |
| `IoC.Scope.Current.Set` | `scope` | `App.ICommand` — сделать скоуп текущим |
| `IoC.Scope.Current.Clear` | – | `App.ICommand` |
| `IoC.Scope.Parent` | – | родитель; у корневого бросает "The root scope has no a parent scope." |

Неизвестный ключ → исключение `Exception("Dependency {dependency} is not found.")` (тесты ловят `Assert.Throws<Exception>`).

**Типовая инициализация теста** (повторяется почти в каждом тест-классе):

```csharp
new InitCommand().Execute();                                                        // scopes.dll
Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", Ioc.Resolve<object>("IoC.Scope.Create")).Execute();  // чистый скоуп на тест
new RegisterIoCDependencyCodeGeneration().Execute();
new RegisterIoCDependencyGeneratedStrategies(typeof(MoveCommand).Assembly, deps).Execute();
new RegisterIoCDependencyMacroCommand().Execute();
new RegisterIoCDependencyActionComposition().Execute();
new RegisterIoCDependencyActionConfigurations(Path.Combine(AppContext.BaseDirectory, "Config")).Execute();
```

`GameScope` (Lib/Scopes) — обёртка над объектом-скоупом: `Enter()` запоминает текущий скоуп, ставит свой и возвращает `IDisposable`,
который при `Dispose` восстанавливает прежний. Так исполняется игра (`Game.Execute`) и так `Server` создаёт потоки/стоп-команды в
скоупе, где регистрировался сервер.

## 3. Модель данных

- `IGameItem` — игровой объект = словарь свойств: `GetProperty(name)`, `SetProperty(name, value)`. `GameItem` — реализация поверх `IDictionary<string,object>`.
  Отсутствующее свойство → `KeyNotFoundException`. Типы значений — по соглашению: `Position`/`Velocity` = `Vector`, `Direction`/`AngularVelocity` = `Angle`, `Fuel`/`Speed`/`Ammo`/`Health`/`Damage` = `int`, `Destroyed` = `bool`, `Id` = `string` (Guid "N").
- `Vector` (`int[] Coordinates`, `Dim`, оператор `+`, value-equality). Конструктор копирует входной массив, но публичный `int[] Coordinates` допускает изменение элементов: полной иммутабельности нет. `new Vector(params int[])`.
- `Angle` — дискретный угол в **восьмых долях круга**: `Denominator = 8`, `Numerator` нормализуется в `[0,8)`, `Radians = 2π·n/8`, `+`, value-equality. `new Angle(n)`.
- `IGameItemStorage` (`Add/Get/Remove/GetSnapshot`, ключ — строковый id) — хранилище объектов боя.
- `IBattleContext` = `Id` + `Storage`. Создаётся `IBattleContextFactory.Create(id)` (`new BattleContext(id, new GameItemStorage())`).
  В скоуп регистрируется `RegisterIoCDependencyBattleContext(context)` → ключи `Contexts.Battle` и `Storages.GameItem`.
- `ICommandContext` — «роль → объект» (`GetItem(role)`); `CommandContext` копирует входные пары (роли регистрозависимы, дубли запрещены).
  Нужен для команд над **двумя** объектами (Target/Source) — столкновения, урон, дозаправка (tanker).

## 4. Паттерн «команда над адаптером» и кодогенерация

Идея учебного курса: команды работают не с `IGameItem` напрямую, а с узким интерфейсом-«представлением»
(`IMoving`, `IRotating`, `IShooting`, …). Адаптер от `IGameItem` к такому интерфейсу **генерируется во время выполнения Roslyn-ом**.

Конвейер (`RegisterIoCDependencyGeneratedStrategies` / `...WithAdapters` → `GeneratedStrategyRegistration`):

1. `CommandFinder.Find(assembly)` — ищет в сборке публичные неабстрактные классы, реализующие `Lib.ICommand`, с именем на `Command`
   и **ровно одним** параметром конструктора, который является «адаптируемым интерфейсом» → пары `(Command, Interface)`.
2. `InterfaceInspector.IsAdaptable(type)` — интерфейс публичный, **не generic**, **только свойства** (никаких методов/событий/индексаторов).
3. `AdapterSourceBuilder` генерирует класс `SpaceBattle.Generated.<Mangled>Adapter : IFoo`, свойства которого ходят в IoC:
   `get → Ioc.Resolve<T>("Properties.<view>.<declaringType>.<Name>", _item)`,
   `set → Ioc.Resolve<Lib.ICommand>("Properties....<Name>.Set", _item, value).Execute()`.
4. `StrategySourceBuilder` генерирует статический класс `SpaceBattle.Generated.Strategies` с методами-стратегиями:
   адаптер `new XAdapter((IGameItem)args[0])` и команду `new SomeCommand(Ioc.Resolve<IFoo>("Adapters.<..>Adapter", args))`.
5. `RoslynSourceCompiler` компилирует исходник в память (кэш по тексту исходника), `Assembly.Load`. В trunk кэш статический; в свежих shooting/server-snapshot и серверных потомках — на уровне экземпляра компилятора, с отдельными тестами изоляции.
6. Регистрация в IoC (в **текущем** скоупе):
   - `Adapters.<Mangled(iface)>Adapter` → адаптер для `args[0]` (IGameItem)
   - `Commands.<Mangled(commandType)>` → команда, обёрнутая вокруг адаптера для `args[0]`
   - `Properties.<view>.<decl>.<Prop>` → геттер (по умолчанию `item.GetProperty(Prop)`; либо перенаправлен через `Dependency`, см. ниже)
   - `Properties.<view>.<decl>.<Prop>.Set` → `SetPropertyCommand(item, Prop, value)`

`GeneratedNames.Mangle(type)` = `FullName.Replace('.', '_').Replace('+', '_')`. Примеры ключей:
`Commands.SpaceBattle_Lib_MoveCommand`, `Adapters.SpaceBattle_Lib_IMovingAdapter`,
`Properties.SpaceBattle_Lib_IMoving.SpaceBattle_Lib_IMoving.Position`.

### `Dependency(Type Interface, string Property, string Strategy)` — вычисляемые/внедряемые свойства
`record Dependency` говорит генератору: «геттер свойства `Property` интерфейса `Interface` не читать из `IGameItem`,
а получить через `Ioc.Resolve<object>(Strategy, item)`». Применения:
- `MovementDependencies.ComputedVelocity`: `IMoving.Velocity` ← `Properties.ComputedVelocity` (скорость вычисляется из `Speed` и `Direction`).
- `ShootingDependencies.All`: `IShooting.ProjectileFactory` ← `Factories.Projectile`, `IShooting.Storage` ← `Storages.GameItem`, `IShooting.ExceptionFactory` ← `Factories.Exception`.
При нескольких кандидатах выбирается наиболее специфичный интерфейс; при неоднозначности — `InvalidOperationException("Ambiguous dependency ...")`.

### Два варианта регистрации
| Класс | Для чего |
|---|---|
| `RegisterIoCDependencyGeneratedStrategies(Assembly, IEnumerable<Dependency>)` | автопоиск команд и их интерфейсов в сборке (публичный API; в актуальном trunk делегирует общий GeneratedStrategyRegistration) |
| `RegisterIoCDependencyGeneratedStrategiesWithAdapters(IEnumerable<Assembly>, IEnumerable<Type> extraInterfaces, IEnumerable<Dependency>)` | то же + явные интерфейсы, для которых нужны адаптеры без одноимённой команды (Target/Source у tanker-refuel и damage) |
| `RegisterIoCDependencyGeneratedAdapters(interfaces, deps)` | только адаптеры, без команд |

Требует предварительно `RegisterIoCDependencyCodeGeneration` (регистрирует `CodeGeneration.*`).

## 5. Действия (Actions): макрокоманды из конфигурации

«Действие» = именованная последовательность команд, заданная **текстовым файлом** `Config/<Action>.config`.

Формат `.config`: токены (имена IoC-ключей) разделяются переводом строки или запятой; всё после `#` в строке — комментарий; пустые игнорируются.

```
# Movement.config
Commands.SpaceBattle_Lib_CheckFuelCommand
Commands.SpaceBattle_Lib_MoveCommand
Commands.SpaceBattle_Lib_BurnFuelCommand
```

`RegisterIoCDependencyActionConfigurations(directory)` для каждого `*.config` (в порядке имён) регистрирует
`<Action>.Config` → `string[]` и `Specs.<Action>` → тот же массив.

Исполнение:
- **`Commands.Action`** (`RegisterIoCDependencyActionComposition`): `Resolve<Lib.ICommand>("Commands.Action", "Movement", item)` → каждое имя из `Movement.Config` резолвится с `item`, всё оборачивается в `Commands.Macro` (`MacroCommand`).
  Макрокоманда выполняет шаги по порядку; исключение на любом шаге **прерывает** остаток (так «нет топлива» не даёт двигаться — Check идёт первым).
- **`Commands.ContextAction`** (`RegisterIoCDependencyContextActionComposition`): `Resolve<Lib.ICommand>("Commands.ContextAction", "Hit", ICommandContext)` — то же, но шаги резолвятся с `ICommandContext` (для команд из двух объектов).
  Шаг-ключи вида `Context.Xxx` регистрируются отдельными `RegisterIoCDependency*ContextCommand` (см. [04-ioc-keys.md](04-ioc-keys.md)).
- `Commands.Macro` (`RegisterIoCDependencyMacroCommand`): `Resolve<Lib.ICommand>("Commands.Macro", cmd1, cmd2, ...)`.

Создание действия **не выполняет** его; команда пере-используется: повторный `Execute()` читает актуальные свойства объекта.

## 6. Игровой цикл и сервер (многопоточность)

```
JSON-строка ──► IMessageReceiver.Receive ──► JsonMessageParser ──► IMessage{GameId,Command,Args}
                         │
                         ▼ GameRegistry.Find(gameId)
                     IGameHost.Send(message)
                         │ new PostToGameCommand(runner, new InterpretMessageCommand(interpreter, message))
                         ▼
                 IServerThread.Add(...)  ─── (очередь входящих BlockingCommandQueue)
                         │  поток ServerThread: Execute() → _behaviour → Process
                         ▼
                 PostToGameCommand.Execute → IGameRunner.Post(cmd) → IGame.Post → GameQueue.Enqueue
                                             + _thread.Schedule(runner)   (планировщик QueueScheduler)
                         ▼
                 GameRunner.Execute → Game.Execute:
                     using scope.Enter()  (игровой IoC-скоуп);  quantum.Start();
                     выполнять команды из GameQueue, пока есть команды и квант не истёк (минимум одна за тик)
                         ▼
                 IoCMessageInterpreter: Ioc.Resolve<ICommand>("Messages.Command.<message.Command>", message).Execute()
```

- **ServerThread** (`Threads/ServerThread.cs`): сам является `ICommand`; `Start()` → `Task.Factory.StartNew(Run, LongRunning)` (повторный старт → исключение).
  Внутренний цикл — вторая очередь `_loop`: поток кладёт себя в `_loop`, `Execute()` выполняет `_behaviour` и, если `_running`, снова ставит себя в `_loop`.
  `Process`: если в планировщике есть команды — взять одну новую из inbox (без блокировки) и одну запланированную; иначе блокирующий `Take()` из inbox.
  Исключения команд ловит `Handle` → `IExceptionHandler.Handle(command, exception)`.
- **Stop-команды**: `HardStopCommand`/`SoftStopCommand` выполняются **только самим потоком** (иначе `InvalidOperation`).
  `HardStop` — закрыть inbox и выйти немедленно; `SoftStop` — закрыть inbox, перейти в `Drain` (дорабатывать оставшееся, потом выйти).
  `Interrupt(cmd)` = добавить в **начало** inbox (используется для hard stop сервера).
- **Routing**: `ConsistentHashRing<TNode>` — кольцо с виртуальными узлами (`replicas`), по умолчанию `FnvHashFunction` (FNV-проход по двум байтам каждого char с финальным перемешиванием); `Sha256HashFunction` остаётся внедряемой альтернативой; иммутабельные снимки + `lock` на запись. Ключ маршрутизации = `gameId`, узел = `IServerThread`.
- **Server** (`Servers/Server.cs`): объекты состояний `CreatedServerState → RunningServerState → ClosedServerState`; публично доступны IsRunning и ThreadCount, прежнего enum ServerState/свойства State в актуальных ветках нет; `Start()` создаёт N потоков (`Thread-0..`), добавляет в кольцо и стартует;
  `AddThread()`; `SoftStop()`/`HardStop()` убирают потоки из кольца, шлют стоп-команды и возвращают `Task`, завершающийся когда все потоки закончили.
- **Games.Start(gameId)** (в `RegisterIoCDependencyMessages`): находит поток по кольцу, создаёт `IGame` (`Games.Create` — свой `BattleContext` и свой скоуп), `IGameRunner`, `GameHost`, и **регистрирует хост в `GameRegistry`**.
- `TimeQuantum(duration, TimeProvider)` — квант считается по `TimeProvider` (в тестах подменяется `ManualTimeProvider`).

В базовых ветках до managed/server-host приложение само регистрирует `ServerThreads.ExceptionHandler`. В свежем server-host это делает composition root: `DeferredExceptionHandler` связывается с `LoggingExceptionHandler` и error recovery. Точка входа существует в `SpaceBattle.Http/Program.cs`; в trunk проекта Http ещё нет.

## 7. Столкновения и урон

- Таблица столкновений — файл `*.collisions`: строки `dx dy dvx dvy` (4 целых; `#`-комментарии и пустые строки игнорируются), где `d = B.pos − A.pos`, `dv = B.vel − A.vel` (`CollisionOffset.Between(first, second)` берёт `Position` и `Velocity` обоих объектов).
- `CollisionTree` — дерево решений (префиксное по 4 числам): `Contains(path)`; строится рекурсивно из списка векторов (без циклов).
- `RegisterIoCDependencyCollisions(directory)`: для каждого `*.collisions` (рекурсивно) регистрирует ленивое дерево по ключу
  `Collisions.<относительный путь без расширения, разделители → '.'>` (напр. `Collisions.Ship-Ship.0-0`) и фабрику `Commands.Collision`.
- `CollisionCommand(tree, Func<Vector> offset, ICommand onCollision)` — если смещение в дереве, выполняет `onCollision`. Смещение вычисляется **в момент Execute**.
- `RegisterIoCDependencyCollisionContextCommand` — контекстная команда: двое по ролям + дерево + имя последствия (`ContextAction`).
- Урон: `ApplyDamageCommand(IDamageTarget, IDamageSource)`: `Health = max(0, Health − Damage)`; `DestroyIfDeadCommand(IDestructible)`: `Health ≤ 0 → Destroyed = true`.
  Цепочка «Hit» (`Config/Hit.config`): `Context.ApplyDamage` → `Context.DestroyIfDead`.
- Генератор таблиц: `tools/collisions/generate_collisions.py` (Python 3; PNG требует Pillow) — Minkowski-разность масок + supercover-проход, `--self-test` для проверки.

## 8. Стрельба

`ShootCommand(IShooting)`: если `Ammo ≤ 0` → `InvalidOperationException("Not enough ammunition to shoot.")`;
иначе `ProjectileFactory.Create(Position, Direction, ProjectileSpeed, ProjectileDamage)` → снаряд (`IGameItem` со свойствами `Id, Position, Direction, Speed, Damage, Destroyed, Velocity`),
кладётся в `Storage` по `Id`, `Ammo--`. Скорость снаряда считается тем же `Properties.ComputedVelocity`.
Фабрика требует двумерную позицию (`Dim == 2`), неотрицательные скорость и урон.
Действие `Shooting` (`Config/Shooting.config` = `Commands.SpaceBattle_Lib_ShootCommand`).


## 9. Современный managed runtime

В `test/server-e2e` и его серверных предках поверх базового сервера построен управляемый runtime. После рефакторинга 2026-11-09 `GameRuntime` — фасад, а логика разнесена по сервисам:

- `ManagedGame` объединяет `Game`, скоуп и состояние исполнения; `GameExecutionState.Paused` останавливает выборку команд после команды-барьера. Снимок получается из потока-владельца игры.
- `ManagedGameHost` хранит игру, runner и поток. `GameHostLifecycle` управляет состояниями `Running`, `Moving`, `Moved`, `Stopped`; `GameHostTransferService` — буфером и проксированием.
- `ManagedGameRegistry` хранит hosts; `GameLifecycle` стартует игры; `GameSnapshotService` координирует снимки/restore; `GameTransferService`, `GameRebalanceService`, `ThreadRemovalService` решают свои задачи через узкие интерфейсы.
- `SemaphoreSlim` жизненного цикла сериализует топологические операции. Внешний перенос использует неблокирующий захват: пересекающиеся операции отклоняются перегрузкой, а не ждут друг друга с взаимной блокировкой.
- `RegisterIoCDependencyManagedGames` создаёт **дочерний текущий скоуп** и переопределяет в нём `Games.Create/Start/Registry`, receiver и interpreter. Он не просто добавляет независимые ключи. При композиции надо сохранять/восстанавливать скоуп.

Снимок игры — JSON с `gameId`, `items`, `commands`. Объекты сериализуются по `Kind` и зарегистрированной схеме свойств; значения — через именованные codecs. Очередь поддерживает только команды с зарегистрированным `ICommandSerializer`. Это не универсальная сериализация любого ICommand/Task/делегата.

Снимок сервера: пауза работающих игр → снимки → сохранение набора → soft stop → состояния Stopped. При ошибке сохранения игры возобновляются, если сервер ещё работает. Restore создаёт managed games и ждёт выполнения барьеров восстановления. `FileSnapshotStore` реализует batch-store и сохраняет весь набор через временный файл/замену; memory replacement-store также заменяет набор, удаляя устаревшие записи.

Внешний перенос: source становится Moving и буферизует новые сообщения; target восстанавливает paused game и возвращает `IGameArrival`; commit передаёт буфер и активирует игру; прежний source становится Moved и проксирует сообщения. До успешного commit ошибка вызывает abort target и resume source. Это протокол `IRemoteServer` в памяти/через внедряемую реализацию, а не готовый сетевой транспорт между произвольными HTTP-серверами.

Добавление потока меняет ring и переносит игры с изменившимся маршрутом. Удаление исключает поток из размещения, эвакуирует игры и завершает поток; удаление последнего потока отклоняется. В новых серверных ветках жизненный цикл `Server` реализован объектами состояний `Servers/States/*`, а ответственность удаления выделена отдельно.

## 10. Конвейер сообщений, ошибки и права

В свежем HTTP-стеке обработка разделена по времени:

```text
POST /messages
  → проверка Bearer JWT и принадлежности gameId
  → whitelist / разбор сообщения / попытка приёма inbox
  → 202 Accepted (команда ещё может не быть выполнена)
  → поток игры, игровой скоуп
  → policy wrapper → player authorization → whitelist → IoC-команда
```

Точный порядок декораторов определяется порядком регистраций: `Messages.Decorate` оборачивает текущий interpreter. В `player-authorization`/E2E права проверяются при Execute, когда доступен storage игры. Асинхронный отказ в исполнении не превращает уже возвращённый 202 в HTTP 403.

`CommandWhitelist` проверяет имена и до постановки, и при интерпретации (важно для restored/проксированных команд). `ExceptionErrorPolicy` выбирает точное правило типа исключения, затем подходящее базовое; default — Skip. Error actions: Retry с ограничением попыток, Skip, StopGame, StopThread. Повтор не откатывает побочные эффекты команды. Потоковые исключения в server-host проходят через отдельно связанный обработчик, а не игнорируются.

JWT: подписанный HS256, issuer/audience/exp, нулевой clock skew, UTF-8 ключ минимум 32 байта; обязательны по одному непустому `sub` и `game`, claim `permission=transfer` даёт право на перенос. `AuthenticatedMessage` переносит player id в очередь и сериализатор снимка, а не доверяет полю игрока из входного JSON.

`player-authorization`: разрешённые команды игрока → целевой id из настроенного аргумента → разрешённые игроку объекты → правила `IAccessRule`. Для существующего объекта стандартное правило проверяет Owner; для CreateItem — Owner создаваемого объекта. Пустые/отсутствующие правила не дают разрешения.

`object-authorization` — **отдельная** модель: объект привязан к access area, area имеет путь предков, grant задаётся субъекту/действию/области. Наследование grant вверх по пути даёт Allow; любое Deny/неизвестное решение запрещает; отсутствие Allow запрещает. Проверка `AuthorizedActionCommand` выполняется перед созданием/исполнением вложенного действия и повторяется при каждом Execute. JWT/HTTP/player rules к этой модели автоматически не подключены.

## 11. HTTP host и композиция

`SpaceBattle.Http` содержит `Program`, `ApiEndpoints`, `HostComposition`, `appsettings.json`. `ServerComposition.Create` создаёт собственный скоуп и вызывает регистрации foundation → runtime → security → пользовательские команды → policies. Затем восстанавливает предыдущий скоуп и возвращает `ServerApplication` с внедрёнными runtime, validator, receiver, snapshot store.

`ServerLifetime : IHostedService` запускает приложение и при остановке ожидает snapshot/soft stop. `ServerOptions` задаёт потоки, inbox, replicas, quantum, приоритеты, игры, схемы, whitelist, JWT и (в player-authorization) права игроков и access rules. `configureCommands` позволяет добавить конкретный протокол команд.

`POST /messages` принимает сообщение, `POST /games/{id}/transfer` — JSON с destination. Ответы: 202/204 при успешном приёме/переносе, 400 для формата, 401 для токена, 403 для синхронного запрета, 404 для неизвестной игры/цели, 503 с Retry-After при перегрузке, 502 при IOException. Внедряемая таблица `IRemoteServer` по умолчанию пуста. Нет endpoint выдачи JWT и автоматической сети server-to-server.

Текущий appsettings — заготовка: JwtSecret пуст по умолчанию, его нужно задать. В player-authorization дополнительно нужны CommandTargets и Players; одного whitelist недостаточно. Детали запуска — [07](07-playbook.md).

## 12. Независимые тики, мир и bounded-load routing

`game-ticks` выросла от trunk, не от `feature/game`: это инфраструктура расписания с callback `Action<ICommand>` для доставки в игру. `DelayedCommandScheduler` использует TimeProvider, один timer, упорядоченные deadline/sequence и отменяемые доставки. `GameTicks` защищается поколением и номером тика от устаревших/повторных команд, ограничивает catch-up, сохраняет Failure при ошибке шага/доставки. Это отдельный механизм от временного бюджета Game.Execute.

`world-update` добавляет снимок списка объектов на начало WorldStep (`frame.Battle`, `frame.Items`), фабрику и действие WorldStep. JSON-конфигурация задаёт порядок фаз `world` (действие над frame) / `items` (действие над отобранными объектами), возможны дополнительные execution modes. Физика, пары столкновений и очистка уничтоженных объектов должны быть зарегистрированы как действия/селекторы; готового соединения со всеми игровыми ветками здесь нет.

`bounded-load-routing` отдельно реализует альтернативу простому ring.Find: лимит `max(1, ceil((totalWithoutKey + 1) / ring.Count * (1+tolerance)))`; пробы ключей `key`, `key/1`, …, выбор первого ниже лимита или наименее загруженного из проверенных. `PlannedMoves` учитывает ещё выполняющиеся перемещения на целевых потоках. Это ограничение по числу игр, не по CPU/длине очереди; число проб конечно, гарантии глобально оптимального размещения нет. Совместимость со свежим серверным стеком требует разрешения двух конфликтов [01](01-branches.md).
