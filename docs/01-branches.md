# 01. Ветки, что в них лежит и в каком порядке сливать

Состояние remote `origin` на 9 ноября 2026. Точные SHA меняются, поэтому перед работой сверяйте `git fetch --all --prune`.

## 1. Главное

1. **Код разделён на два направления**, и в одну ветку они пока не собраны:
   - **ствол игровых команд** — `foundation/command-infrastructure`;
   - **серверный стек** — цепочка PR от `server-thread` до `server-host`, плюс надстройки сверху (`bounded-load-routing`, `test/server-e2e`).
2. `main` содержит базовую инфраструктуру (IoC-регистрации, генерация адаптеров, действия из `.config`, предметы, хранилище, контекст боя). Серверного стека и игровых команд в `main` пока нет.
3. Серверные ветки **стековые**: каждая основана на предыдущей. После правки нижней ветки все верхние перебазируются.
4. Игровые команды (движение, стрельба и др.) в серверный стек не входят, поэтому HTTP-приложение само по себе умеет только `CreateItem`. Остальные команды регистрируются приложением ([04-ioc-keys.md](04-ioc-keys.md) §F).

## 2. Авторы

| Автор | Основная работа |
|---|---|
| `stwz13` | серверный стек, сериализация, предметы, перенос, ребаланс, политика ошибок, JWT, HTTP, маршрутизация, CI |
| `Yaroslav` | foundation: командная инфраструктура, вычисляемая скорость, фабрика предметов, хранилище, контекст боя, вращение |
| `maxximben` | стрельба (`feature/shooting`) |

## 3. Серверный стек (слияние снизу вверх)

Каждая строка основана на предыдущей. Порядок слияния — сверху вниз по таблице.

| № | PR | Ветка | Что добавляет |
|---|---|---|---|
| 1 | #15 | `feature/server-thread` | серверный поток, очередь, планировщик, `SoftStop`/`HardStop` |
| 2 | #16 | `feature/routing` | кольцо консистентного хэширования, `Routing.Ring`, `Routing.Thread` |
| 3 | #14 | `feature/game` | игра, очередь команд, квант времени, скоуп игры |
| 4 | #17 | `feature/game-runner` | `GameRunner`: игра выполняется на серверном потоке |
| 5 | #18 | `feature/messages` | разбор JSON-сообщений, приём, `GameHost`, `GameRegistry`, `Games.Start` |
| 6 | #19 | `feature/server-management` | `Server` с состояниями, запуск, добавление потока, мягкая и жёсткая остановка |
| 7 | #21 | `feature/game-snapshot` | снимок игры в JSON и восстановление |
| 8 | #22 | `feature/game-items` | предметы с `Id` и `Kind`, схемы видов, `CreateItem` |
| 9 | #25 | `feature/server-snapshot` | снимок и восстановление всех игр сервера |
| 10 | #26 | `feature/game-transfer` | перенос игры на другой сервер с буферизацией сообщений |
| 11 | #27 | `feature/thread-rebalance` | перераспределение игр при добавлении потока |
| 12 | #28 | `feature/remove-thread` | эвакуация игр и удаление потока |
| 13 | #29 | `feature/priority-queue` | приоритетные очереди с сохранением порядка |
| 14 | #30 | `feature/command-whitelist` | белый список команд из конфигурации |
| 15 | #31 | `feature/error-policy` | политики ошибок: повтор, пропуск, остановка игры или потока |
| 16 | #32 | `feature/auth` | проверка JWT игрока и игры |
| 17 | #33 | `feature/http-api` | HTTP: сообщения и перенос игры |
| 18 | #34 | `feature/server-host` | сборка настраиваемого сервера, снимок при остановке; в эту ветку влит #35 |
| — | #35 | `feature/player-authorization` | права игрока на команды и предметы (влита в `server-host`) |
| — | #36 | `test/server-e2e` | сквозной тест жизненного цикла с авторизацией |
| — | #38 | `feature/bounded-load-routing` | маршрутизация с ограничением нагрузки на поток (основана на `server-host`) |

Заметки:
- #14 исторически основан на `routing`, а #17 — на `game`. После перестройки стека базы PR нужно поставить именно так.
- #38 не зависит от #35 и #36, их порядок между собой любой.
- Если ветка #38 показывает конфликты в `ServerComposition.cs` и `ServerOptions`, значит, она основана на старой версии `server-host`. Перебазируйте её: `git rebase --onto origin/feature/server-host <старая-основа> <ветка>`. Строку регистрации `RegisterIoCDependencyBoundedLoadRouting` и поля `LoadTolerance`/`LoadProbes` нужно оставлять в новых местах: `RegisterIoCDependencyServerRuntime` и `Runtime/ServerOptions.cs`.

## 4. Ствол игровых команд — `foundation/command-infrastructure`

Включает всю инфраструктуру и уже слитые в него игровые ветки.

| Тема | Что есть |
|---|---|
| Инфраструктура | предметы, хранилище, контекст боя, вычисляемая скорость, генерация адаптеров, действия из `.config`, макрокоманды |
| Движение и топливо | `MoveCommand`, `CheckFuelCommand`, `BurnFuelCommand`, `Config/Movement.config` |
| Вращение | `RotateCommand`, `Config/Rotation.config` |
| Дозаправка | `RefuelCommand` (цель и источник), контекстная команда, `Config/Refuel.config` |
| Стрельба | `ShootCommand`, `ProjectileFactory`, `Config/Shooting.config` |
| Столкновения | дерево решений, загрузка таблиц `*.collisions`, `CollisionCommand`, генератор таблиц `tools/collisions` |
| Тики игры | `GameTicks`, `DelayedCommandScheduler`, `StartGameTicksCommand`, `StopGameTicksCommand` |
| Объектная авторизация | `Authorization/*`: дерево областей, наследуемые разрешения, `AuthorizedActionCommand` |
| Поток | `ServerThread`, очереди, `HardStop`/`SoftStop` (старая реализация, на ней основан серверный стек) |

Отдельные ветки, которых нет в стволе:

| Ветка | Что добавляет |
|---|---|
| `feature/damage` (⊃ `feature/collisions`) | `ApplyDamageCommand`, `DestroyIfDeadCommand`, `Config/Hit.config` |
| `feature/world-update` и `feature/game-ticks` | шаг мира из фаз (`WorldStep`), загрузка фаз из JSON, `ForEachWorldItemCommand` |
| `feature/game-ticker` | более ранний вариант тиков |
| `feature/object-authorization` | то же, что `Authorization/*` выше (влита в ствол) |

`feature/movement-fuel`, `feature/rotation`, `feature/shooting` и `feature/tanker-refuel` влиты в ствол и остались как отдельные ветки.

## 5. Как сливать

1. Сначала `foundation/command-infrastructure` → `main` (если ещё не слита).
2. Серверный стек — снизу вверх по таблице §3, merge-коммитом или rebase-merge. **Не squash**: иначе следующей ветке придётся заново перебазироваться.
3. После каждого слияния меняйте базу следующего PR на `main`. Можно сразу выстроить базы всей цепочки, а потом сливать по очереди.
4. Игровые ветки (`damage`, `world-update`) независимы от сервера и вливаются в любой момент после ствола.
5. Конфликты между серверным стеком и стволом возможны там, где обе стороны определяют одни и те же типы: `ServerThread`, очереди, `IExceptionHandler`, `ServerThreadDependencies`. Решайте при интеграции.

## 6. Как посмотреть ветку, не трогая рабочую копию

```bash
git fetch --all --prune
git log --graph --oneline --simplify-by-decoration --remotes=origin     # граф веток
git diff --name-status origin/main origin/feature/<имя> -- SpaceBattle  # что добавляет ветка
mkdir /tmp/snap && git archive origin/feature/<имя> | tar -x -C /tmp/snap # снимок дерева
```

Сборку и тесты запускайте в отдельной копии (`git worktree add` или клон), а не в рабочей папке.
