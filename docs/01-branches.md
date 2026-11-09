# 01. Все ветки: состав, зависимости, совместимость

Срез 2026-11-09. Источник — `git ls-remote --heads origin`, локальные refs, `git log`, `git ls-tree`, чтение исходников и пробные слияния в scratch-клоне. **На старте 37 удалённых веток**: 30 `feature/*`, 5 `foundation/*`, `main`, `test/server-e2e`. При финальной сверке feature/refuel отсутствует на GitHub: **36 актуальных удалённых веток** (29 feature) и один сохранённый локальный remote-tracking снимок. Историческая refuel также полностью рассмотрена; исходные refs намеренно не prune-ились. Полные SHA сохранены в [audit/branches-2026-11-09.json](audit/branches-2026-11-09.json).

## 1. Полный реестр

Везде ниже опущен `origin/`. «Предки» — ближайшие по достижимости **текущие tips веток**, а не название исходной ветки при её создании. Если их два, это два независимых включённых tip. Исторические ветки после rebase могут быть содержательно похожи без отношения предок/потомок.

| Ветка | Tip | Ближайшие tips-предки | Содержимое/изменения |
|---|---|---|---|
| `foundation/battle-context` | `3b6cc77` | `foundation/game-item-factory` | Исторические storage и battle context поверх game-item-factory. |
| `foundation/command-infrastructure` | `b51def2` | `foundation/battle-context`, `foundation/specific-command-infrastructure` | Общий trunk: объединены battle-context и обновлённая specific-command-infrastructure; PR #39/#40 есть в истории. |
| `foundation/computed-velocity` | `8381159` | `main` | Базовая командная инфраструктура и вычисление скорости; VelocityCalculation выделен из регистрации. |
| `foundation/game-item-factory` | `39a8b4a` | `main` | Историческая фабрика игровых объектов (старая линия foundation). |
| `foundation/specific-command-infrastructure` | `5646e4b` | `foundation/computed-velocity` | Контекстные действия, явные адаптеры; включает текущую computed-velocity. |
| `feature/auth` | `b7c19a3` | `feature/error-policy` | JWT HS256, привязка игрока к игре, сериализация authenticated messages. |
| `feature/bounded-load-routing` | `90c7c2d` | `feature/game-items` | Самостоятельная линия server-host + ограничение нагрузки и учёт запланированных перемещений; свежий player-authorization отсутствует. |
| `feature/collisions` | `7c645ae` | `foundation/command-infrastructure` | Деревья столкновений, таблицы и Python-генератор; контекстная команда. |
| `feature/command-whitelist` | `b42da92` | `feature/priority-queue` | Whitelist до приёма сообщения и при интерпретации; композиция декораторов. |
| `feature/damage` | `8d0cb28` | `feature/collisions` | Наследует collisions; урон, уничтожение, Hit.config. |
| `feature/error-policy` | `700fab2` | `feature/command-whitelist` | Retry/Skip/StopGame/StopThread; отдельные error actions/recovery и execution state. |
| `feature/game` | `26ca0ee` | `feature/routing` | Game, TimeQuantum, скоуп игры; теперь потомок routing. |
| `feature/game-items` | `b0be228` | `feature/game-snapshot` | Создание объектов с Id/Kind и регистрация схем свойств. |
| `feature/game-runner` | `7f53126` | `feature/game` | GameRunner/PostToGame поверх game; async-тесты. |
| `feature/game-snapshot` | `fe35341` | `feature/server-management` | JSON игры: id, объекты по схемам Kind, сериализуемые команды; codecs. |
| `feature/game-ticks` | `db73f2a` | `foundation/command-infrastructure` | Монотонное расписание тиков, catch-up limit, отмена и обработка ошибок доставки; отдельна от сервера. |
| `feature/game-transfer` | `a40ebcd` | `feature/server-snapshot` | Двухфазный перенос с буфером, rollback и прокси старого host. |
| `feature/http-api` | `f7806fd` | `feature/auth` | SpaceBattle.Http; POST /messages и /games/{id}/transfer, HTTP-тесты. |
| `feature/messages` | `525244d` | `feature/game-runner` | JSON → game registry → game host → команда; контроль перегрузки. |
| `feature/movement-fuel` | `27db2f8` | `foundation/command-infrastructure` | CheckFuel → Move → BurnFuel; привязка Velocity к вычисляемой скорости. |
| `feature/object-authorization` | `66f9e3a` | `foundation/command-infrastructure` | Отдельная линия прав: дерево областей, привязка объектов, grants и deny-overrides policy. |
| `feature/player-authorization` | `02734d6` | `feature/server-host` | Правила команд/объектов игрока, Owner, конфигурируемые IAccessRule; не дерево областей. |
| `feature/priority-queue` | `33e9de7` | `feature/remove-thread` | Стабильная очередь: больший Priority раньше; FIFO при равенстве. |
| `feature/refuel` **(удалена на GitHub в ходе аудита; локальный снимок)** | `7b0a4e0` | `main` | Старый вариант: один объект с Fuel/FuelReserve, проверка запаса перед дозаправкой. |
| `feature/remove-thread` | `fd59855` | `feature/thread-rebalance` | Эвакуация игр и удаление потока с восстановлением при ошибке. |
| `feature/rotation` | `69c05b3` | `foundation/command-infrastructure` | RotateCommand, Rotation.config; создание Angle напрямую. |
| `feature/routing` | `4920733` | `feature/server-thread` | Кольцо с виртуальными узлами и Routing.Thread(gameId). |
| `feature/server-host` | `c5b4067` | `feature/http-api` | Композиция из ServerOptions, hosted lifetime, файл снимков, восстановление; обработчик ошибок потока. |
| `feature/server-management` | `e6dd947` | `feature/messages` | Server, состояния и жизненный цикл потоков; теперь прямой потомок messages. |
| `feature/server-snapshot` | `0c034dc` | `feature/game-items` | Managed runtime, пауза/снимки всех игр/restore; реестр, lifecycle и изоляция кэша. |
| `feature/server-thread` | `306c5de` | `foundation/command-infrastructure` | Поток, inbox с TryAdd/capacity, scheduler, hard/soft stop; async-тесты. |
| `feature/shooting` | `3ff48cf` | `foundation/command-infrastructure` | Снаряды и боезапас; свежий SOLID-рефакторинг и изоляция кэша Roslyn; отдельна от серверного стека. |
| `feature/tanker-refuel` | `aa87762` | `foundation/command-infrastructure` | Другой вариант: Target/Source, частичная передача min(amount,reserve). |
| `feature/thread-rebalance` | `0bf3785` | `feature/game-transfer` | Добавление потока и перенос игр по изменившемуся маршруту. |
| `feature/world-update` | `d4ed09d` | `feature/game-ticks` | Наследует game-ticks; WorldStep frame и конфигурация фаз world/items; не готовая физика. |
| `main` | `a3aecbf` | — | Пустой каркас: CI, решение, два csproj и DLL; игрового кода нет. |
| `test/server-e2e` | `496c961` | `feature/player-authorization` | Наследует player-authorization; E2E двух игроков, перегрузка/перенос/снимки/restore/stop и архитектурный тест runtime. |

## 2. Граф актуального стека

```text
main a3aecbf
├─ историческая foundation/game-item-factory → battle-context ─┐
├─ обновлённая computed-velocity → specific-command ───────────┴─ trunk b51def2
│   ├─ movement-fuel
│   ├─ rotation
│   ├─ shooting
│   ├─ tanker-refuel
│   ├─ collisions → damage
│   ├─ game-ticks → world-update
│   ├─ object-authorization
│   └─ server-thread → routing → game → game-runner → messages
│       → server-management → game-snapshot → game-items
│           ├─ server-snapshot → game-transfer → thread-rebalance → remove-thread
│           │   → priority-queue → command-whitelist → error-policy → auth
│           │   → http-api → server-host → player-authorization → test/server-e2e
│           └─ самостоятельный пересобранный серверный стек → bounded-load-routing
└─ refuel (историческое ответвление от 0e79e07)
```

Это схема tips и направлений развития, не полный commit DAG. `bounded-load-routing` содержит серверные возможности уровня host по коду, но tip `server-host` не является его предком. Поэтому нельзя считать, что он автоматически включает рефакторинги от 2026-11-09.

`main` остаётся каркасом; термин trunk здесь означает `foundation/command-infrastructure`, а не официальную default branch (default — `main`). Даты коммитов в истории: 2026-10-11…2026-11-09. Есть авторы `stwz13`, `Yaroslav`, `maxximben`; авторство коммитов не доказывает распределение заданий или личность пользователя. Стрельба содержит работу `maxximben`.

## 3. Локальные ветки

На момент аудита текущая ветка — `main`, рабочий код не checkout-ился. Локальных веток **14**: `main`, `feature/auth`, `feature/command-whitelist`, `feature/error-policy`, `feature/game-transfer`, `feature/http-api`, `feature/player-authorization`, `feature/priority-queue`, `feature/remove-thread`, `feature/server-host`, `feature/server-snapshot`, `feature/shooting`, `feature/thread-rebalance`, `test/server-e2e`.

Каждая имеет тот же SHA, что одноимённая `origin/*`; дополнительных локальных вариантов кода нет. `AGENTS.md`, `CLAUDE.md`, `docs/` изначально были untracked. Ни исходные ветки, ни пользовательские refs аудитом не менялись.

## 4. Что изменилось относительно документации 2026-11-02

- Веток 37 вместо 19; trunk `b51def2`, shooting `3ff48cf`, server-management `e6dd947`.
- Старое описание `messages`/`server-management` как конкурирующих дублированных стеков больше не применимо: актуальный `messages` — предок актуального `server-management`.
- Актуальный `game` тоже предок серверного стека. Его не нужно повторно сливать; прежний конфликт GameDependencies относится к старым tips.
- Добавлены сеть/точка входа, аутентификация, снимки, размещение игр, политики ошибок и несколько независимых расширений. Прежнее «в репозитории отсутствуют» неверно.
- Устранено дублирование пути GeneratedStrategies; часть веток отдельно исправляет статический кэш компилятора.
- Числа тестов и покрытия заменены свежими результатами [06](06-status.md).

## 5. Проверенные слияния 2026-11-09

В **scratch-клоне**, начиная с `foundation/command-infrastructure`, последовательно слиты:

```text
test/server-e2e
feature/shooting
feature/movement-fuel
feature/rotation
feature/tanker-refuel
feature/damage
feature/world-update
feature/object-authorization
```

Все восемь слияний прошли **без текстовых конфликтов**. `collisions` уже входит в `damage`, `game-ticks` — в `world-update`, серверные предки — в E2E. Но тесты этого состава: **809/810**, падает ServerRuntimeArchitectureTests из-за `new Delivery` в Scheduling/DelayedCommandScheduler. Подробности — [06](06-status.md). Это экспериментальная интеграция для проверки, не новая опубликованная ветка и не подтверждение автоматического подключения игровых фич в ServerComposition.

Тот же состав **без world-update/game-ticks** проверен отдельно: **729/729 тестов прошли**. Это подтверждённый зелёный набор исходников, но для запуска всех игровых действий через HTTP ещё нужны прикладные регистрации и конфигурация.

После этого отдельно проверены и отменены конфликтующие попытки (refuel ещё была доступна на старте, затем удалена на GitHub):

| Добавляемая ветка | Конфликты |
|---|---|
| `feature/bounded-load-routing` | `SpaceBattle.Lib/Registration/ServerComposition.cs`, `SpaceBattle.Lib/Runtime/ServerApplication.cs` |
| `feature/refuel` | `SpaceBattle.Lib/Commands/RefuelCommand.cs`, `SpaceBattle.Lib/Config/Refuel.config`, `SpaceBattle.Tests/Tests/RefuelCommandTests.cs`, `SpaceBattle.Tests/Tests/RefuelTests.cs` |

Пути в таблице — относительно каталога решения `SpaceBattle/`. Конфликты bounded-load не разрешались: нужна осмысленная адаптация маршрутизатора к свежим runtime services и композиции. Два refuel имеют разную семантику и одинаковые имена, поэтому требуется выбрать/согласовать реализацию.

Не проверялись все попарные комбинации 37 веток. Отсутствие конфликта не заменяет тесты, ручную проверку регистрации действий и анализ семантики.

## 6. Просмотр и воспроизведение

В пользовательском клоне безопасны read-only команды `git show origin/<branch>:<path>`, `git diff origin/A origin/B`, `git log --all --graph --oneline`, `git ls-tree -r --name-only origin/<branch>`. Новые refs, checkout, merge, сборки и тесты — в scratch-клоне по [07](07-playbook.md).
