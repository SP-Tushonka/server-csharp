using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using SPTarkov.Server.Web;

namespace UnitTests.Tests.Web;

[TestFixture]
public class ModRouteValidatorTests
{
    private readonly IServiceProvider _services = new ServiceCollection().AddLogging().BuildServiceProvider();
    
    private static readonly MethodInfo _blazorCreateRouteTable = typeof(IComponent)
        .Assembly.GetType("Microsoft.AspNetCore.Components.RouteTableFactory")!
        .GetMethod("Create", BindingFlags.NonPublic | BindingFlags.Static, [typeof(List<Type>), typeof(IServiceProvider)])!;

    private static readonly object[] _routeSets =
    [
        new object[] { new[] { typeof(Plain), typeof(PlainTrailingSlash) }, false },
        new object[] { new[] { typeof(Plain), typeof(PlainUpperCase) }, false },
        new object[] { new[] { typeof(Plain), typeof(Other) }, true },
        new object[] { new[] { typeof(Id), typeof(Name) }, false },
        new object[] { new[] { typeof(Id), typeof(IntId) }, true },
        new object[] { new[] { typeof(IntId), typeof(IntName) }, false },
        new object[] { new[] { typeof(Plain), typeof(OptionalId) }, true },
        new object[] { new[] { typeof(Id), typeof(OptionalId) }, false },
        new object[] { new[] { typeof(CatchAll), typeof(Id) }, true },
        new object[] { new[] { typeof(Unparsable) }, false },
        new object[] { new[] { typeof(UnknownConstraint) }, false },
        new object[] { new[] { typeof(TwoRoutesOnePage) }, true },
    ];

    [TestCaseSource(nameof(_routeSets))]
    public void FindRouteError_AgreesWithBlazorsRouteTable(Type[] components, bool valid)
    {
        var error = ModRouteValidator.FindRouteError(components, _services);

        Assert.That(error is null, Is.EqualTo(valid), error);
        Assert.That(BlazorAccepts(components), Is.EqualTo(valid));
    }

    [Test]
    public void FindRouteError_SicPages_AreAccepted()
    {
        var components = ModRouteValidator.GetRoutableComponents([typeof(SPTWeb).Assembly]);

        Assert.That(components, Is.Not.Empty);
        Assert.That(ModRouteValidator.FindRouteError(components, _services), Is.Null);
        Assert.That(BlazorAccepts(components), Is.True);
    }

    private bool BlazorAccepts(IEnumerable<Type> components)
    {
        try
        {
            _blazorCreateRouteTable.Invoke(null, [components.ToList(), _services]);

            return true;
        }
        catch (TargetInvocationException)
        {
            return false;
        }
    }

    [Route("/moda")]
    public class Plain : ComponentBase;

    [Route("/moda/")]
    public class PlainTrailingSlash : ComponentBase;

    [Route("/MODA")]
    public class PlainUpperCase : ComponentBase;

    [Route("/modb")]
    public class Other : ComponentBase;

    [Route("/moda/{id}")]
    public class Id : ComponentBase;

    [Route("/moda/{name}")]
    public class Name : ComponentBase;

    [Route("/moda/{id:int}")]
    public class IntId : ComponentBase;

    [Route("/moda/{name:int}")]
    public class IntName : ComponentBase;

    [Route("/moda/{id?}")]
    public class OptionalId : ComponentBase;

    [Route("/moda/{*rest}")]
    public class CatchAll : ComponentBase;

    [Route("/moda/{id")]
    public class Unparsable : ComponentBase;

    [Route("/moda/{id:nope}")]
    public class UnknownConstraint : ComponentBase;

    [Route("/moda")]
    [Route("/moda/settings")]
    public class TwoRoutesOnePage : ComponentBase;
}
