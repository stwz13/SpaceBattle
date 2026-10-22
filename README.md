# SpaceBattle

Библиотека игровых команд на .NET 10. Зависимости и адаптеры регистрируются в IoC,
последовательности команд описываются в файлах `Config/*.config`.

## Выстрел

Действие `Shooting` создаёт снаряд в текущей позиции корабля и списывает один патрон.
Направление берётся с корабля, скорость и урон — из `ProjectileSpeed` и
`ProjectileDamage`. Скорость корабля к скорости снаряда не прибавляется.
При `Ammo <= 0` возникает `InvalidOperationException`; при ошибке создания или
добавления снаряда боезапас не расходуется. Задержки между выстрелами нет.

Снаряд — отдельный `IGameItem` с уникальным `Id` и свойствами `Position`,
`Direction`, `Speed`, `Velocity`, `Damage`, `Destroyed`. Позиция копируется,
чтобы изменение координат корабля не перемещало уже выпущенные снаряды.
Скорость вычисляется существующей зависимостью `ComputedVelocityDependencies.Velocity`.
Позиция должна быть двумерной, скорость и урон — неотрицательными.
Углы имеют восемь направлений (`Angle.Denominator == 8`).

Пример инициализации и выстрела в scope одного боя:

```csharp
using App;
using App.Scopes;
using SpaceBattle.Lib;

new InitCommand().Execute();
Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set",
    Ioc.Resolve<object>("IoC.Scope.Create")).Execute();

new RegisterIoCDependencyExceptionFactory().Execute();
new RegisterIoCDependencyGameItemFactory().Execute();
new RegisterIoCDependencyComputedVelocity().Execute();
new RegisterIoCDependencyProjectileFactory().Execute();
new RegisterIoCDependencyCodeGeneration().Execute();
new RegisterIoCDependencyGeneratedStrategies(
    typeof(ShootCommand).Assembly, ShootingDependencies.All).Execute();
new RegisterIoCDependencyMacroCommand().Execute();
new RegisterIoCDependencyActionComposition().Execute();
new RegisterIoCDependencyActionConfigurations(
    Path.Combine(AppContext.BaseDirectory, "Config")).Execute();

var battle = new BattleContextFactory().Create("Battle-1");
new RegisterIoCDependencyBattleContext(battle).Execute();
var ship = Ioc.Resolve<IGameItemFactory>(GameItemFactoryDependencies.Factory)
    .Create(new Dictionary<string, object>
    {
        ["Position"] = new Vector(12, 5),
        ["Direction"] = new Angle(0),
        ["ProjectileSpeed"] = 10,
        ["ProjectileDamage"] = 25,
        ["Ammo"] = 3
    });
battle.Storage.Add("Ship-1", ship);

Ioc.Resolve<SpaceBattle.Lib.ICommand>("Commands.Action", "Shooting", ship).Execute();
// Ammo == 2; снаряд находится в battle.Storage, Velocity == (10, 0).
```

В приложении с несколькими боями `RegisterIoCDependencyBattleContext` вызывается
в отдельном scope каждого боя. Существующая команда при исполнении использует
хранилище текущего scope. При регистрации других генерируемых команд объединяйте
их описания зависимостей с `ShootingDependencies.All`.

Для контекстного действия используйте существующий
`RegisterIoCDependencyContextCommand("Context.Shoot", GeneratedNames.CommandDependency(typeof(ShootCommand)), "Shooter")`
и укажите `Context.Shoot` в конфигурации контекстного действия.

Команда выстрела создаёт и регистрирует снаряд. Его дальнейшее движение,
проверка столкновений и применение урона подключаются соответствующими механиками.
Исполнение команд одного боя должно быть последовательным.

## Проверка

```shell
dotnet test SpaceBattle/SpaceBattle.slnx --configuration Release
```
