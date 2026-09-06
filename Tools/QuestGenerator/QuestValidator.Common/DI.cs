using System;
using Microsoft.Extensions.DependencyInjection;
using SPTarkov.DI;
using SPTarkov.Server.Core.Utils;

namespace QuestValidator.Common;

// Resolves services out of the server libraries without booting a server.
public class DI
{
    private static DI? _instance;

    private readonly IServiceProvider _serviceProvider;

    private DI()
    {
        var services = new ServiceCollection();
        var handler = new DependencyInjectionHandler(services);
        handler.AddInjectableTypesFromTypeAssembly(typeof(DI));
        handler.AddInjectableTypesFromTypeAssembly(typeof(JsonUtil));
        handler.InjectAll();
        _serviceProvider = services.BuildServiceProvider();
    }

    public static DI GetInstance()
    {
        return _instance ??= new DI();
    }

    public T GetService<T>()
        where T : notnull
    {
        return _serviceProvider.GetRequiredService<T>();
    }
}
