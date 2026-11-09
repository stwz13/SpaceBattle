# AGENTS.md — ООАиП, проект SpaceBattle

Инструкции для любых ИИ-агентов, работающих в этой папке. Полная документация — в `docs/` внутри корня репозитория; инструкции и документация включены в git. Актуализировано 2026-11-09 по 37 снимкам веток: 36 актуальных GitHub и историческая refuel, удалённая на сервере за время аудита.

## Где что лежит
- Корень этой папки — клон `https://github.com/stwz13/SpaceBattle`; вложенная `SpaceBattle/` — каталог решения (C#, .NET 10, xUnit). Ветка `main` содержит каркас; общий ствол — `origin/foundation/command-infrastructure`, наиболее полный серверный стек — `origin/test/server-e2e`. Игровые фичи и независимые расширения распределены по другим веткам, см. полный реестр в docs.
- `docs/README.md` — входная точка и индекс. Далее по необходимости: `01-branches.md` (ветки, слияния), `02-architecture.md`, `03-modules.md`, `04-ioc-keys.md`, `05-conventions-and-ci.md`, `06-status.md`, `07-playbook.md` (рецепты).

## Правила работы
- Не коммитить, не пушить, не переключать ветки и не менять исходники в пользовательском клоне без явной просьбы пользователя. Сборку, тесты и слияния выполнять в scratch-клоне (`docs/07-playbook.md` §1). Обновление документации не означает разрешения на правки кода или публикацию.
- Задание преподавателя в репозитории не найдено. Недостающие требования спрашивать у пользователя, не выдумывать.
- Утверждение «тесты проходят» подкреплять свежим прогоном `dotnet test` (для английского вывода: `DOTNET_CLI_UI_LANGUAGE=en VSLANG=1033`).
- Общаться с пользователем по-русски.

## Ограничения кода (проверяются тестами)
- В `SpaceBattle.Lib` запрещены `for` и `while` (разрешены `foreach`, LINQ, рекурсия).
- В папках `Commands`, `ValueObjects`, `Threads`, `Receivers`, `Processors`, `Behaviours`, `Queues` запрещён прямой доступ к `Ioc`.
- В проекте два `ICommand`: `App.ICommand` (из `core.dll`, только для IoC) и `SpaceBattle.Lib.ICommand` (все игровые команды). Не путать.
- `core.dll` и `scopes.dll` — готовые библиотеки преподавателя, не менять.
- В `test/server-e2e` дополнительно проверяется отсутствие IoC и создания объектов (кроме исключений) в `Runtime`, `Authentication`, `Scheduling`. Ветки с тиками текстово сливаются, но нарушают это дополнительное ограничение; см. свежий статус интеграции в docs/06-status.md.

## Команды
```bash
cd SpaceBattle                                              # из корня scratch-клона, папка с SpaceBattle.slnx
dotnet build SpaceBattle.slnx -c Release
dotnet test SpaceBattle.Tests/SpaceBattle.Tests.csproj --collect:"XPlat Code Coverage"
```
