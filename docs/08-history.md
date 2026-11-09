# 08. Выполненные задачи и порядок их выполнения

Список идёт в том порядке, в каком задачи делались и оформлялись PR (по номерам PR и истории веток). Каждая следующая задача опирается на предыдущие. Номера PR указаны там, где они подтверждены историей git; пропуски в нумерации — PR, не вошедшие в этот перечень. Период работы: с 11 октября по 9 ноября 2026. Порядок задач внутри этапа 2 примерный: эти ветки развивались параллельно от основы.

## Этап 1. Основа

Автор — `Yaroslav`, если не указано иное.

| № | PR | Ветка | Задача | Результат |
|---|---|---|---|---|
| 1 | #5 | `foundation/computed-velocity` | вычисляемая скорость | `Properties.ComputedVelocity`: скорость из `Speed` и `Direction` |
| 2 | #9 | `foundation/specific-command-infrastructure` | инфраструктура конкретных команд | команда над адаптером, кодогенерация, регистрация стратегий |
| 3 | — | `foundation/game-item-factory` | фабрика предметов | `IGameItemFactory` и регистрация в IoC |
| 4 | #12 | ветка хранилища предметов | хранилище предметов | `IGameItemStorage`, `GameItemStorage` |
| 5 | #13 | `foundation/battle-context` | контекст боя | `IBattleContext`, фабрика, регистрации в скоупе |
| 6 | #39, #40, #1 | foundation → `main` | слияние основы в `main` | `main` получил общую инфраструктуру |

## Этап 2. Игровые команды

| № | PR | Ветка | Задача | Результат |
|---|---|---|---|---|
| 7 | — | `feature/movement-fuel` | движение и топливо | `MoveCommand`, `CheckFuelCommand`, `BurnFuelCommand`, `Movement.config` |
| 8 | — | `feature/rotation` | вращение | `RotateCommand`, `Rotation.config` |
| 9 | — | `feature/tanker-refuel` | дозаправка двух предметов | `RefuelCommand(target, source)`, контекстная команда, `Refuel.config` |
| 10 | — | `feature/shooting` (`maxximben`) | стрельба | `ShootCommand`, `ProjectileFactory`, `Shooting.config` |
| 11 | — | `feature/collisions` | столкновения | дерево решений, загрузка таблиц, генератор |
| 12 | #10 | `feature/damage` | урон и уничтожение | `ApplyDamageCommand`, `DestroyIfDeadCommand`, `Hit.config` |
| 13 | #37 | `feature/object-authorization` | объектная авторизация | области доступа, наследуемые разрешения |
| 14 | #24 | `feature/world-update` / `feature/game-ticks` | тики и шаг мира | `GameTicks`, фазы шага мира из JSON |

## Этап 3. Базовый серверный стек

| № | PR | Ветка | Задача | Результат |
|---|---|---|---|---|
| 15 | #15 | `feature/server-thread` | серверный поток | очередь, планировщик, `SoftStop`, `HardStop` |
| 16 | #16 | `feature/routing` | маршрутизация игр по потокам | кольцо консистентного хэширования |
| 17 | #14 | `feature/game` | игра | очередь команд, квант времени, скоуп игры |
| 18 | #17 | `feature/game-runner` | выполнение игры на потоке | `GameRunner`, `PostToGameCommand` |
| 19 | #18 | `feature/messages` | сообщения | JSON-разбор, приём, `GameHost`, `GameRegistry` |
| 20 | #19 | `feature/server-management` | управление сервером | запуск, добавление потока, остановка, состояния |

## Этап 4. Развитие сервера

| № | PR | Ветка | Задача | Результат |
|---|---|---|---|---|
| 21 | #21 | `feature/game-snapshot` | снимок игры | JSON-снимок и восстановление, теги типов значений |
| 22 | #22 | `feature/game-items` | предметы с `Id` и `Kind` | `Items.Create`, схемы видов, `CreateItem` |
| 23 | #25 | `feature/server-snapshot` | снимок всего сервера | `SnapshotAndStop`, `Restore`, `FileSnapshotStore` |
| 24 | #26 | `feature/game-transfer` | перенос игры | буфер сообщений, прокси-хост, отмена при ошибке |
| 25 | #27 | `feature/thread-rebalance` | перераспределение игр | `AddThread` с переездом игр |
| 26 | #28 | `feature/remove-thread` | удаление потока | эвакуация игр и остановка потока |
| 27 | #29 | `feature/priority-queue` | приоритеты команд | устойчивая приоритетная очередь |
| 28 | #30 | `feature/command-whitelist` | белый список команд | `AllowedCommands`, отказ вне списка |
| 29 | #31 | `feature/error-policy` | политика ошибок | `Retry`, `Skip`, `StopGame`, `StopThread`, журнал |
| 30 | #32 | `feature/auth` | JWT | проверка игрока и игры |
| 31 | #33 | `feature/http-api` | HTTP | `POST /messages`, `POST /games/{id}/transfer` |
| 32 | #34 | `feature/server-host` | сборка сервера | `ServerComposition`, настройки, снимок при остановке |
| 33 | #35 | `feature/player-authorization` | права игрока | команды, предметы, правила владения |
| 34 | #36 | `test/server-e2e` | сквозной тест | авторизованный жизненный цикл сервера |

## Этап 5. Рефакторинг и качество

Выполнено поверх стека, без изменения поведения:

1. **Тесты без блокировок.** Во всех тестах стека `.Wait()` заменён на `async`/`await` (xUnit1031).
2. **Устранено дублирование** кода между PR игры и PR раннера игры.
3. **Сервер на паттерне «Состояние».** Правила переходов вынесены в классы `Created`, `Running`, `Closed` (`Servers/States/`).
4. **Перечисление `ServerState` удалено.** Состояние выражается самими классами, интерфейс разделён на `IServerLifecycle`, `IServerThreads`, `IServerAvailability`. Верхние ветки стека перестроены поверх.
5. **Ревью по SOLID.** Классы runtime и безопасности разделены на узкие сервисы (`GameLifecycle`, `GameSnapshotService`, `GameTransferService`, `GameRebalanceService`, `ThreadRemovalService`, `ServerComposition` из трёх регистраций); правки внесены в те коммиты, где код появился.

## Этап 6. Расширение маршрутизации

| № | PR | Ветка | Задача | Результат |
|---|---|---|---|---|
| 35 | #38 | `feature/bounded-load-routing` | ограничение нагрузки на поток | `BoundedLoadRouter`, `PlacementLoadMeter`, опции `LoadTolerance`, `LoadProbes`; перебазирована на обновлённый `server-host` |

## Этап 7. Слияние

1. Серверный стек слит снизу вверх по цепочке PR (#15 … #36) в `feature/server-host`; базы PR выстроены в порядке стека.
2. В ствол `foundation/command-infrastructure` влиты игровые ветки (движение, вращение, дозаправка, стрельба, объектная авторизация, тики).
3. PR #38 перебазирован на обновлённый `server-host` и ждёт слияния.

Что ещё не сделано — в [06-status.md](06-status.md) §3–§5.
