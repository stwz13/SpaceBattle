# 07. Рецепты

## 0. Правила работы

- Не коммитить, не пушить и не переключать ветки без явной просьбы. Эксперименты, сборку и тесты делать в отдельной копии (`git worktree add ../wt <ветка>` или клон).
- Работаем по-русски; недостающие вводные уточняем, а не придумываем.
- Утверждение «работает» подтверждается запуском тестов с итогом (сколько прошло и упало).
- Изменения оформляем по [05-conventions-and-ci.md](05-conventions-and-ci.md) §3: Conventional Commits, без удалённых строк в PR, стек перебазируем при правке нижней ветки.

## 1. Запустить HTTP-сервер

Проект `SpaceBattle.Http` входит в решение ветки `main`.

```bash
cd SpaceBattle
export Server__JwtSecret="замените-на-секрет-не-короче-32-байт-0000"
export Server__Players__p1__Commands__0=CreateItem
export Server__Players__p1__Items__0='*'
dotnet run --project SpaceBattle.Http -- --urls http://localhost:5000
```

Настройки — секция `Server` в `SpaceBattle.Http/appsettings.json` (переопределяются переменными окружения вида `Server__ThreadCount`):

| Параметр | Значение по умолчанию | Смысл |
|---|---|---|
| `ThreadCount` | 2 | число серверных потоков |
| `InboxCapacity` | 1024 | размер входящего ящика потока; также буфера переноса |
| `RingReplicas` | 64 | виртуальных узлов на поток в кольце |
| `QuantumMilliseconds` | 10 | квант времени игры |
| `PriorityQueues` | true | приоритетные очереди вместо обычных |
| `LoadTolerance` | не задан | включает ограничение нагрузки (`bounded-load-routing`), допуск, например `0.25` |
| `LoadProbes` | 16 | число попыток подбора потока при ограничении нагрузки |
| `Games` | `["demo"]` | игры, запускаемые при старте |
| `AllowedCommands` | `["CreateItem"]` | белый список команд |
| `CommandTargets` | `{"CreateItem":"id"}` | команда → имя поля `args` с id целевого предмета |
| `Players` | `{}` | `{ "p1": { "Commands": ["CreateItem"], "Items": ["*"] } }` |
| `CommandAccessRules` | `{}` | команда → список правил доступа (`existing-owner`, `new-owner`, свои) |
| `ItemKinds` | `{"Ship":["Fuel","Owner"]}` | вид → свойства для снимка |
| `SnapshotOnStop`, `RestoreOnStart` | true | снимок при остановке и восстановление при старте |
| `SnapshotPath` | `data/server-snapshot.json` | файл снимка |
| `JwtIssuer`, `JwtAudience`, `JwtSecret` | `SpaceBattle`, `Players`, пусто | параметры JWT; секрет не короче 32 байт |

## 2. Получить токен игрока и отправить сообщение

Токен — JWT HS256 с `iss`/`aud` из настроек, обязательным сроком (`exp`) и утверждениями: `sub` (игрок), `game` (игра), необязательно `permission = transfer`. Сервиса выдачи токенов в проекте нет; для проверки токен можно создать так:

```csharp
var handler = new Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler();
var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(secret));
var token = handler.CreateToken(new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor {
    Issuer = "SpaceBattle", Audience = "Players",
    Expires = DateTime.UtcNow.AddHours(1),
    Subject = new System.Security.Claims.ClaimsIdentity(new[] {
        new System.Security.Claims.Claim("sub", "p1"),
        new System.Security.Claims.Claim("game", "demo") }),
    SigningCredentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(key, "HS256") });
```

Запрос:
```bash
curl -X POST http://localhost:5000/messages \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"gameId":"demo","command":"CreateItem","args":{"kind":"Ship","id":"s1","properties":{"Fuel":{"type":"int","value":10},"Owner":{"type":"string","value":"p1"}}}}'
```
Ответ `202` значит «принято в очередь игры», а не «выполнено». Чтобы команда прошла, она должна быть в `AllowedCommands`, у игрока в `Players.<id>.Commands`, а в `CommandTargets` — иметь поле с id предмета. Коды ошибок — [03-modules.md](03-modules.md) §3.18.

## 3. Собрать сервер в тесте или приложении

```csharp
var app = ServerComposition.Create(
    new ServerOptions { ThreadCount = 2, Games = ["demo"], AllowedCommands = ["Echo"],
                        JwtSecret = "…не короче 32 байт…" },
    snapshots: new MemorySnapshotStore(new ConcurrentDictionary<string, string>()),
    configureCommands: () => Ioc.Resolve<App.ICommand>("IoC.Register",
        MessageDependencies.CommandKey("Echo"), (Func<object[], object>)(args => new MyEchoCommand((IMessage)args[0]))).Execute());
await app.Start();
// ... работа ...
await app.Stop();
```

`ServerComposition.Create` сам регистрирует всё нужное. Команды протокола приложение добавляет в `configureCommands` (он вызывается до регистрации прав и политики ошибок).

## 4. Добавить новую команду игры (по образцу движения)

1. `Interfaces/IFoo.cs` — интерфейс-представление: **только свойства**, не generic. Внедряемые зависимости (фабрики, хранилище) — тоже свойства.
2. `Commands/FooCommand.cs` — `public class FooCommand(IFoo foo) : ICommand` (один параметр, имя оканчивается на `Command`); без `Ioc` и без `for`/`while`.
3. `Config/Foo.config` — ключи шагов, например `Commands.SpaceBattle_Lib_FooCommand` (константа — `GeneratedNames.CommandDependency(typeof(FooCommand))`). Файл копируется в вывод автоматически.
4. Если нужны подставляемые зависимости — `Dependencies/FooDependencies.cs` с `Dependency(typeof(IFoo), nameof(IFoo.X), "<ключ IoC>")`.
5. Тест `Tests/FooTests.cs`: конструктор поднимает скоуп и регистрирует `InitCommand → Scope.Create/Set → CodeGeneration → GeneratedStrategies(typeof(FooCommand).Assembly, deps) → Macro → ActionComposition → ActionConfigurations(dir)`; вызов — `Commands.Action`, `"Foo"`, `item`. Проверить: успех, повторный `Execute`, отсутствие свойства (`KeyNotFoundException`, предмет не изменён), крайние значения.
6. Чтобы команда принималась по HTTP: зарегистрировать `Messages.Command.Foo` (создаёт команду из `IMessage`), добавить `Foo` в `AllowedCommands`, `CommandTargets`, права игрока.

### Запустить движение через очередь игры

Подключите стандартное действие `Movement`. Для вычисления скорости по `Speed` и `Direction` зарегистрируйте `RegisterIoCDependencyComputedVelocity` и передайте `MovementDependencies.ComputedVelocity` при регистрации генерируемых стратегий.

```csharp
// В скоупе игры на потоке-владельце очереди.
new RegisterIoCDependencyLongOperations().Execute();
new RegisterIoCDependencyLongMovement(new CommandReceiver(game.Post)).Execute();
var movement = Ioc.Resolve<LongOperationCommands>(MovementDependencies.CreateOperation, item);
game.Post(movement.Start);
```

Один шаг сдвигает предмет на его текущую скорость и списывает топливо. Чтобы остановить движение, поставьте `movement.Stop` в ту же очередь. После остановки для повторного запуска используйте ту же пару команд. Если другой компонент обрабатывает траектории, передайте его метод записи вторым аргументом регистрации длительного движения.

## 5. Команда над двумя предметами (по образцу дозаправки / урона)

1. Два интерфейса (`IXxxTarget`, `IXxxSource`), команда `XxxCommand(target, source)`.
2. Адаптеры: `RegisterIoCDependencyGeneratedStrategiesWithAdapters([asm], [typeof(IXxxTarget), typeof(IXxxSource)], deps)`.
3. `RegisterIoCDependencyXxxContextCommand(depKey, targetRole, sourceRole)` по образцу `RegisterIoCDependencyRefuelContextCommand`.
4. `Config/Xxx.config` = `Context.Xxx`, вызов — `Commands.ContextAction`.

## 6. Новый вид предмета и новый тип значения в снимке

- Вид: `new RegisterIoCDependencyItemKind("Ship", ["Fuel", "Owner"]).Execute()` или `ServerOptions.ItemKinds`. В снимок попадут только перечисленные свойства.
- Тип значения: `new RegisterIoCDependencyValueType<T>("тег", value => JsonNode, node => T).Execute()`. Тип без регистрации при снимке → `NotSupportedException`.
- Новый вид команды в снимке: класс `: ICommandSerializer` (`Tag`, `Handles`, `Write`, `Read`) и добавление в список `Serialization.CommandSerializers`.

## 7. Новое состояние сервера

Наследуйте `OpenServerState` (открытое состояние с общей остановкой) или `ServerStateBase` и реализуйте `IsRunning`, `Start`, `AddThread`, `Stop`. Переход — через `IServerContext.Become(...)` из соседнего состояния. Правила переходов живут в классах состояний, а не в `Server`.

## 8. Новая регистрация IoC

В `SpaceBattle.Lib` создайте `Registration/RegisterIoCDependencyXxx : ICommand` и вызовите `IoCRegistrar.Register(Key, strategy)` из `Execute()`. Общий метод регистрирует стратегию в текущем скоупе. Ключ храните в `Dependencies/XxxDependencies.cs`. Стратегия проверяет число и типы аргументов и выдаёт `ArgumentException` при неверном вызове. В тесте регистрации проверьте разрешение ключа, неверные аргументы и поведение в нужном скоупе.

## 9. Перестроить стек PR после правки нижней ветки

```bash
git branch backup/<имя>-before <ветка>            # резервная ссылка
git rebase --onto <новая-основа> <старая-основа> <ветка>
# повторить для каждой ветки выше, подставляя перебазированную основу
git push --force-with-lease origin <ветка>
```
Конфликт при перебазировании: взять версию основы (`git checkout --ours`, в rebase «ours» = основа), вернуть собственные правки ветки, `git add`, `git rebase --continue`. После каждого шага проверять `git diff --numstat <основа> HEAD | awk '$2>0'` — удалённых строк быть не должно.

## 10. Посмотреть, что принесла ветка

```bash
git fetch --all --prune
git log --graph --oneline --simplify-by-decoration --remotes=origin
git diff --name-status origin/main origin/feature/<имя> -- SpaceBattle
git merge-base --is-ancestor origin/A origin/B && echo "A ⊂ B"
git log origin/B --not origin/A --oneline        # есть в B, нет в A
```

## 11. Запустить длительную операцию

В примере `game` — игра, а `move` и `checkCollisions` — готовые игровые команды. Соберите один шаг как макрокоманду и создайте пару команд управления.

```csharp
new RegisterIoCDependencyLongOperations().Execute();
var step = new MacroCommand([move, checkCollisions]);
var operation = Ioc.Resolve<LongOperationCommands>(
    LongOperationDependencies.Create, step, new CommandReceiver(game.Post));
game.Post(operation.Start);
```

Сохраните `operation` и при необходимости поставьте `operation.Stop` в ту же очередь. Вызывайте `Game.Post` на её потоке-владельце. При запуске нескольких операций создайте отдельную пару для каждой. Если шаг должен повторяться с заданным интервалом, используйте планирование по времени. Немедленное добавление в хвост даёт порядок FIFO.

## 12. Подключить тики игры

Штатный `Tick.config` включает фазу `World.CheckCollisions`. В примере `collisionTablesDirectory` содержит таблицу `Ship-Torpedo.collisions`, а предикаты `isShip` и `isTorpedo` выбирают участников в ролях `Target` и `Source`.

```csharp
// В скоупе игры с зарегистрированным IBattleContext.
new RegisterIoCDependencyMacroCommand().Execute();
new RegisterIoCDependencyActionConfigurations(Path.Combine(AppContext.BaseDirectory, "Config")).Execute();
new RegisterIoCDependencyLongOperations().Execute();
new RegisterIoCDependencyWorldTicks().Execute();
new RegisterIoCDependencyCollisions(collisionTablesDirectory).Execute();
new RegisterIoCDependencyContextActionComposition().Execute();
new RegisterIoCDependencyCodeGeneration().Execute();
new RegisterIoCDependencyGeneratedStrategiesWithAdapters([typeof(DestroyIfDeadCommand).Assembly],
    [typeof(IDamageTarget), typeof(IDamageSource)], []).Execute();
new RegisterIoCDependencyDamageContextCommand("Context.ApplyDamage", "Target", "Source").Execute();
new RegisterIoCDependencyContextCommand("Context.DestroyIfDead",
    GeneratedNames.CommandDependency(typeof(DestroyIfDeadCommand)), "Target").Execute();
new RegisterIoCDependencyWorldCollisions(isShip, isTorpedo, "Collisions.Ship-Torpedo", "Hit").Execute();
var ticks = Ioc.Resolve<LongOperationCommands>(GameTickDependencies.Create, new CommandReceiver(game.Post));
game.Post(ticks.Start);
```

Порядок фаз задаёт файл `Tick.config` в зарегистрированном каталоге. Чтобы переставить или исключить фазу, измените список ключей. Чтобы добавить фазу, зарегистрируйте ключ, принимающий `IReadOnlyCollection<IGameItem>` и возвращающий `ICommand`, затем впишите его в конфиг. Конфиг читается при выполнении каждого тика. Пустой список фаз даёт пустой шаг, а длительная операция продолжает ставить тики до остановки.

Команда, которая переместила объект, передаёт в фазу его фактические начальную и конечную позиции. Записывайте один участок на объект за интервал тика из той же игровой очереди.

```csharp
var segments = Ioc.Resolve<WorldMovementSegments>(WorldCollisionDependencies.MovementSegments);
segments.Record(item, start, end);
```

Для остановки поставьте `ticks.Stop` в ту же очередь. Для одного шага разрешите `Ioc.Resolve<ICommand>(GameTickDependencies.Step)` и поставьте полученную команду отдельно. Игра выполняет команды на потоке-владельце своего скоупа и очереди.
