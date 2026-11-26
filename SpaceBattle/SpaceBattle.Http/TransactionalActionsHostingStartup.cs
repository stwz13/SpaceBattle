using SpaceBattle.Lib;

[assembly: HostingStartup(typeof(SpaceBattle.Http.TransactionalActionsHostingStartup))]

namespace SpaceBattle.Http;

public sealed class TransactionalActionsHostingStartup : IHostingStartup
{
    public void Configure(IWebHostBuilder builder) =>
        builder.ConfigureServices((context, services) =>
        {
            var enabled = context.Configuration.GetValue<bool>("Transactions:Enabled");
            services.AddSingleton<ITransactionCommandEffect, ProjectileInsertionEffect>();
            services.AddSingleton<IGameRuleParameter>(provider =>
                new ActionTransactionsRule(enabled, provider.GetServices<ITransactionCommandEffect>()));
        });
}