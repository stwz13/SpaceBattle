# 01. Текущая ветка и история интеграции

Состояние `main` на 20 ноября 2026. Таблицы ниже описывают происхождение компонентов.

## 1. Главное

1. В `main` объединены серверный стек и игровые команды. PR #43 включил серверный хост, ограничение нагрузки, движение, стрельбу, столкновения, урон, тики и шаг мира.
2. Серверные ветки создавались стеком. Таблица в §3 показывает исторический порядок зависимостей.
3. Штатный HTTP-хост принимает `CreateItem`. Для подключения других команд используются регистрации и настройки из [04-ioc-keys.md](04-ioc-keys.md) §F и [07-playbook.md](07-playbook.md) §4.

## 2. Авторы

| Автор | Основная работа |
|---|---|
| `stwz13` | серверный стек, сериализация, предметы, перенос, ребаланс, политика ошибок, JWT, HTTP, маршрутизация, CI |
| `Yaroslav` | foundation: командная инфраструктура, вычисляемая скорость, фабрика предметов, хранилище, контекст боя, вращение |
| `maxximben` | стрельба (`feature/shooting`) |

## 3. История серверного стека

Строки с порядковыми номерами показывают последовательность развития стека. Дополнительные ветки указаны в конце.

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

PR #38 вошёл в историю `main` через объединение в PR #43. В действующей композиции `RegisterIoCDependencyServerRuntime` включает ограничение нагрузки при заданном `ServerOptions.LoadTolerance`.

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

Связанные исторические ветки:

| Ветка | Что добавляет |
|---|---|
| `feature/damage` (⊃ `feature/collisions`) | `ApplyDamageCommand`, `DestroyIfDeadCommand`, `Config/Hit.config` |
| `feature/world-update` и `feature/game-ticks` | шаг мира из фаз (`WorldStep`), загрузка фаз из JSON, `ForEachWorldItemCommand` |
| `feature/game-ticker` | более ранний вариант тиков |
| `feature/object-authorization` | то же, что `Authorization/*` выше (влита в ствол) |

`feature/movement-fuel`, `feature/rotation`, `feature/shooting` и `feature/tanker-refuel` влиты в ствол и остались как отдельные ветки.

## 5. Как работать с новым стеком PR

Первую ветку нового стека основывайте на актуальной `main`, каждую следующую — на предыдущей. Сливайте снизу вверх merge-коммитом или rebase-merge. После каждого слияния меняйте базу следующего PR на `main`. Команды для перестроения стека приведены в [07-playbook.md](07-playbook.md) §9.

## 6. Как посмотреть ветку, не трогая рабочую копию

```bash
git fetch --all --prune
git log --graph --oneline --simplify-by-decoration --remotes=origin     # граф веток
git diff --name-status origin/main origin/feature/<имя> -- SpaceBattle  # что добавляет ветка
mkdir /tmp/snap && git archive origin/feature/<имя> | tar -x -C /tmp/snap # снимок дерева
```

Сборку и тесты запускайте в отдельной копии (`git worktree add` или клон), а не в рабочей папке.
