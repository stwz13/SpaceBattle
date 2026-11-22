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
| `AllowedCommands` | `["CreateItem","FleetOrder"]` | белый список команд |
| `CommandTargets` | `{"CreateItem":"id","FleetOrder":"id"}` | команда → имя поля `args` с id целевого предмета |
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

## 11. Согласовать правила партии

Правило действия задаёт имя и упорядоченную последовательность шагов. Например, для выбранной партии «АварийныйПрыжок» может состоять из штатного перемещения и шага из будущего модуля.

```json
{
  "actions": {
    "АварийныйПрыжок": ["Commands.SpaceBattle_Lib_MoveCommand", "Mods.Warp"]
  },
  "parameters": {
    "objects.Ship": { "speed": 3, "turnSteps": 2 },
    "objects.Probe": { "turnSteps": 1 },
    "commands.Mods.Warp": { "distance": 8 },
    "Shooting.Projectile": { "speed": 8, "damage": 4 }
  }
}
```

`objects.Ship.speed` задаёт постоянную скорость предметов вида `Ship`. Допустимо целое число от 0 до 2147483647. Смещение вычисляется по текущему направлению и округляется по правилам сетки. Прежний ключ `Ship.Speed` используется, если в `objects.Ship` не указано поле `speed`.

`turnSteps` задаёт угол одного поворота в шагах по 45°. В примере поворот корабля меняет направление на 90°, а поворот зонда на 45°. В другом виде можно использовать те же поля, указав его `Kind` в имени `objects.<вид>`. Неизвестное поле объекта возвращает HTTP 400 при предложении правил.

Поля задаёт серверная схема `Config/ObjectRules.json`. Чтобы открыть ещё одно читаемое свойство адаптера, добавьте запись с `view`, `property` и `type`. Необязательный `min` задаёт нижнюю границу целочисленного входа. Схема по умолчанию копируется вместе со сборкой. Другой файл можно указать через настройку хоста `GameRules:ObjectSchema`.

`commands.Mods.Warp` передаёт параметры будущему шагу `Mods.Warp`. Форму и допустимые значения определяет модуль этого шага. Неизвестные параметры сохраняются вместе с согласованными правилами. Когда появится фабрика, она прочитает их при построении команды. В примере с `distance` предполагается, что фабрика поддерживает именно такое поле.

В предложении можно передать только `parameters`, только `actions` или оба раздела. Прежний плоский формат списка действий также принимается. Каждое предложение заменяет весь набор. Изменение скорости требует нового подтверждения от каждого участника. Согласованные значения восстанавливаются из снимка вместе с действиями.

`Shooting.Projectile` задаёт скорость и урон создаваемых снарядов. Оба значения — целые неотрицательные числа. При выстреле `ShootCommand` получает их через свойства `IShooting.ProjectileSpeed` и `IShooting.ProjectileDamage`. Боезапас, позицию и направление команда по-прежнему получает от стреляющего объекта.

Для параметров другого класса модуль может зарегистрировать через DI `GameClassParameter<TSettings>` как `IGameRuleParameter`. Регистрация задаёт публичное имя параметра, чистую функцию чтения и проверки JSON, а также действие, подключающее типизированные настройки в текущем скоупе игры. В качестве `TSettings` можно указать узкий интерфейс потребителя. Код клиента зависит только от его свойств. На этапе предложения фабрика настроек проверяет данные без изменения игрового состояния. При старте или восстановлении устанавливается принятая версия.

Каждый запрос использует Bearer JWT участника с идентификатором этой игры. Последовательность для новой партии `match`:

1. Участник отправляет `POST /games/match/rules/lobby` с JSON-массивом идентификаторов, например `["p1","p2"]`. Состав фиксируется при создании.
2. Любой участник отправляет весь набор правил через `PUT /games/match/rules`. Ответ содержит номер версии `revision`. Новое предложение сбрасывает все подтверждения.
3. Участники читают предложение через `GET /games/match/rules`. Каждый подтверждает одну версию запросом `POST /games/match/rules/{revision}/confirm`.
4. После подтверждения всеми участниками любой из них вызывает `POST /games/match/rules/{revision}/start`. Повторный старт той же версии возвращает уже созданную игру. Последующие изменения правил отклоняются.

Имена команд можно согласовать заранее. Приложение подключает реализацию шага под тем же именем через `RegisterIoCDependencyRuleCommand`. Конфигурация определяет порядок, а контракт реализации определяет входные данные, изменения состояния и допустимые ошибки. Композиция сначала разрешает все шаги. При отсутствующей реализации сообщает её имя. Если исключение возникло уже в `Execute`, предыдущие шаги могли изменить состояние.

Предложения и подтверждения до старта хранятся в памяти. Снимок запущенной партии содержит согласованный набор имён и шагов. При восстановлении сервер заново связывает их с реализациями в скоупе игры. Для вызова действия через игровой HTTP-протокол применяется существующая регистрация обработчика сообщения и его прав (§4).

Для команд зарегистрируйте результат `RegisterIoCDependencyRuleCommand.For<TInput, TTarget, TSettings>(name, adapt, factory, read, defaults)` как singleton. Хост подключит и фабрику, и проверку её настроек. Фабрики с одним аргументом `IRuleCommandFactory<TTarget>` по-прежнему регистрируются через перегрузку без настроек.

## 12. Сформировать флотилии и отправить приказ

Массив `["p1","p2"]` при создании лобби задаёт участников. После подтверждения правил при старте сервер создаёт область для флотилии каждого участника и выдаёт права в её пределах. Состав определяется свойством `Owner` предметов. Для добавления двух кораблей во флотилию `p1` отправьте два `CreateItem` с разными `id`, видом `Ship` и `properties.Owner = {"type":"string","value":"p1"}`. Проверка создания сверяет владельца с JWT. При восстановлении сохраните `Owner` и свойства нужных действий в `Server:ItemKinds`.

Добавьте `FleetOrder` в `Server:Players:<игрок>:Commands` и разрешите нужные идентификаторы кораблей в `Items`. Штатный HTTP-хост уже добавляет сообщение в белый список и читает цель из `args.id`. Приказ в `POST /messages` с JWT игрока для `match` имеет форму:

```json
{"gameId":"match","command":"FleetOrder","args":{"id":"s1","action":"Rotation"}}
```

HTTP 202 подтверждает постановку в очередь. При выполнении проверяется дерево прав флотилии. Отказ не меняет игровой объект и передаётся в серверную политику ошибок. Права на собственные корабли восстанавливаются из снимка вместе с правилами.

Для заправки отправьте тот же `FleetOrder`. Основной `id` указывает заправщик в роли `Source`, а `roles.Target` указывает корабль-получатель:

```json
{
  "gameId": "match",
  "command": "FleetOrder",
  "args": {
    "id": "p1-tanker-1",
    "action": "Refuel",
    "roles": { "Target": "p1-s1" }
  }
}
```

Оба объекта должны быть доступны игроку по настройкам хоста и входить в его флотилию. Имена ролей учитывают регистр. Для нового контекстного действия зарегистрируйте `FleetContextOrder` в DI с именем действия, ролью основного объекта и остальными ролями. Сами шаги задаются обычной конфигурацией действий или в согласованном JSON.

## 13. Проверить завершение боя

Запустите согласованную партию из двух участников и создайте корабли обеих флотилий. Для стрельбы передайте `FleetOrder` с действием `Shooting`. Обычный `Tick.config` задаёт фазы до коллизий, проверки коллизий и после коллизий. Снаряд летит отдельной длительной операцией в той же FIFO-очереди. Список видов кораблей задаётся `Server:BattleShipKinds` (по умолчанию `Ship` и `Tanker`).

Запрос `GET /games/match/result` с JWT для `match` возвращает состояние партии и победителя. После выбывания всей флотилии `p2` ответ имеет вид:

```json
{"hasStarted":true,"isFinished":true,"winner":"p1"}
```

Для демонстрации с перезапуском укажите в `Server:ItemKinds` все свойства кораблей, используемые в сценарии. Например, для стрельбы и попаданий сохраняйте `Owner`, `Position`, `Direction`, `Health`, `Destroyed`, `Ammo`, `ProjectileSpeed` и `ProjectileDamage`. Схема снаряда есть в штатной конфигурации. Незавершённый бой после восстановления продолжает тики, завершённый сохраняет победителя.

## 14. Запустить длительную операцию

В примере `game` — игра, а `move` и `checkCollisions` — готовые игровые команды. Соберите один шаг как макрокоманду и создайте пару команд управления.

```csharp
new RegisterIoCDependencyLongOperations().Execute();
var step = new MacroCommand([move, checkCollisions]);
var operation = Ioc.Resolve<LongOperationCommands>(
    LongOperationDependencies.Create, step, new CommandReceiver(game.Post));
game.Post(operation.Start);
```

Сохраните `operation` и при необходимости поставьте `operation.Stop` в ту же очередь. Вызывайте `Game.Post` на её потоке-владельце. При запуске нескольких операций создайте отдельную пару для каждой. Если шаг должен повторяться с заданным интервалом, используйте планирование по времени. Немедленное добавление в хвост даёт порядок FIFO.

## 15. Подключить тики игры

Штатный `Tick.config` включает фазу `World.CheckCollisions` и команды до и после неё. При отдельном подключении команды до и после коллизий по умолчанию пусты. В примере `collisionTablesDirectory` содержит таблицу `Ship-Torpedo.collisions`, а предикаты `isShip` и `isTorpedo` выбирают участников в ролях `Target` и `Source`.

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
