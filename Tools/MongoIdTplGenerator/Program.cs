using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MongoIdTplGenerator.Utils;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI;
using SPTarkov.Server.Core.Loaders;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Services.Hosted;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Helpers;

namespace MongoIdTplGenerator;

public class Program
{
    public static async Task Main(string[] args)
    {
        try
        {
            ProgramStatics.Initialize();

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(typeof(ISptLogger<>), typeof(SptBasicLogger<>));
            serviceCollection.AddHttpContextAccessor();
            serviceCollection.AddHttpClient();
            foreach (var (type, config) in await ConfigLoader.Initialize())
            {
                serviceCollection.AddSingleton(type, config);
            }
            serviceCollection.AddSingleton(WebApplication.CreateBuilder());
            serviceCollection.AddSingleton<IReadOnlyList<SptMod>>([]);
            var diHandler = new DependencyInjectionHandler(serviceCollection);

            diHandler.AddInjectableTypesFromTypeAssembly(typeof(Program));
            diHandler.AddInjectableTypesFromTypeAssembly(typeof(SPTStartupHostedService));

            diHandler.InjectAll();

            serviceCollection.AddSingleton(ProgramHelpers.CreateEarlyLocaleTable());
            serviceCollection.AddSingleton<DatabaseImporter>();
            var tables =
                await serviceCollection.BuildServiceProvider().GetRequiredService<DatabaseImporter>().LoadDatabaseAsync(false)
                ?? throw new InvalidOperationException("Database failed to load");
            tables.AddToServices(serviceCollection);
            var serviceProvider = serviceCollection.BuildServiceProvider();

            await serviceProvider.GetService<Application>()?.Run()!;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}
