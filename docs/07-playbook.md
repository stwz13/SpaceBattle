# 07. Рецепты

## 0. Правила работы

- Не коммитить, не пушить и не переключать ветки без явной просьбы. Эксперименты, сборку и тесты делать в отдельной копии (`git worktree add ../wt <ветка>` или клон).
- Работаем по-русски; недостающие вводные уточняем, а не придумываем.
- Утверждение «работает» подтверждается запуском тестов с итогом (сколько прошло и упало).
- Изменения оформляем по [05-conventions-and-ci.md](05-conventions-and-ci.md) §3: Conventional Commits, без удалённых строк в PR, стек перебазируем при правке нижней ветки.

## 1. Запустить HTTP-сервер

Нужна ветка с серверным стеком (`feature/server-host` или выше), в ней проект `SpaceBattle.Http`.

```bash
cd SpaceBattle
export Server__JwtSecret="замените-на-секрет-не-короче-32-байт-0000"
dotnet run --project SpaceBattle.Http
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
| `Games` | `[]` | игры, запускаемые при старте |
| `AllowedCommands` | `[]` | белый список команд |
| `CommandTargets` | `{}` | команда → имя поля `args` с id целевого предмета |
| `Players` | `{}` | `{ "p1": { "Commands": ["CreateItem"], "Items": ["*"] } }` |
| `CommandAccessRules` | `{}` | команда → список правил доступа (`existing-owner`, `new-owner`, свои) |
| `ItemKinds` | `{}` | вид → свойства для снимка (`{"Ship": ["Fuel","Owner"]}`) |
| `SnapshotOnStop`, `RestoreOnStart` | false | снимок при остановке и восстановление при старте |
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

Класс `Registration/RegisterIoCDependencyXxx : ICommand`, в `Execute()` — `Ioc.Resolve<App.ICommand>("IoC.Register", Key, (object[] args) => …).Execute()` (внутри Lib можно короче: `IoCRegistrar.Register`, он internal). Ключ — константа в `Dependencies/XxxDependencies.cs`. Проверять `args.Length` и типы, бросать `ArgumentException`. Тест регистрации: ключ резолвится, неверные аргументы → `ArgumentException`, поведение внутри нужного скоупа.

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
