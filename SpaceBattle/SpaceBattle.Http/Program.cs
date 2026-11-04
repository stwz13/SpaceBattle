namespace SpaceBattle.Http;

public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        HostComposition.Configure(builder.Services, builder.Configuration);
        var app = builder.Build();
        ApiEndpoints.Map(app);
        app.Run();
    }
}
