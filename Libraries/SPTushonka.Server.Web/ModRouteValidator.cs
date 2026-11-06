using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Constraints;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.Options;

namespace SPTarkov.Server.Web;

public static class ModRouteValidator
{
    public static List<Type> GetRoutableComponents(IEnumerable<Assembly> assemblies)
    {
        return assemblies
            .SelectMany(assembly => assembly.ExportedTypes)
            .Where(type =>
                typeof(IComponent).IsAssignableFrom(type)
                && type.IsDefined(typeof(RouteAttribute))
                && !type.IsDefined(typeof(ExcludeFromInteractiveRoutingAttribute))
            )
            .ToList();
    }

    public static string? FindRouteError(IEnumerable<Type> components, IServiceProvider services)
    {
        var options = Options.Create(new RouteOptions());
        options.Value.SetParameterPolicy("regex", typeof(RegexInlineRouteConstraint));
        var constraintResolver = new DefaultInlineConstraintResolver(options, services);
        var routes = new List<ParsedRoute>();

        foreach (var component in components)
        {
            foreach (var attribute in component.GetCustomAttributes<RouteAttribute>(inherit: false))
            {
                RoutePattern pattern;
                try
                {
                    pattern = RoutePatternFactory.Parse(attribute.Template);
                }
                catch (RoutePatternException exception)
                {
                    return $"The route '{attribute.Template}' in '{component.FullName}' is invalid. {exception.Message}";
                }

                var unknownConstraint = pattern
                    .Parameters.SelectMany(parameter => parameter.ParameterPolicies)
                    .FirstOrDefault(policy => policy.Content is not null && constraintResolver.ResolveConstraint(policy.Content) is null);
                if (unknownConstraint is not null)
                {
                    return $"The route '{attribute.Template}' in '{component.FullName}' uses the unknown constraint '{unknownConstraint.Content}'";
                }

                var route = new ParsedRoute(component, pattern, RoutePrecedence.ComputeInbound(new RouteTemplate(pattern)));
                var clash = routes.FirstOrDefault(existing => IsAmbiguous(existing, route));
                if (clash is not null)
                {
                    return $"The following routes are ambiguous: '{clash.Pattern.RawText?.Trim('/')}' in '{clash.Component.FullName}' and '{pattern.RawText?.Trim('/')}' in '{component.FullName}'";
                }

                routes.Add(route);
            }
        }

        return null;
    }
    
    private static bool IsAmbiguous(ParsedRoute first, ParsedRoute second)
    {
        var firstSegments = first.Pattern.PathSegments;
        var secondSegments = second.Pattern.PathSegments;
        if (first.Precedence != second.Precedence || firstSegments.Count != secondSegments.Count)
        {
            return false;
        }

        for (var segment = 0; segment < firstSegments.Count; segment++)
        {
            var firstParts = firstSegments[segment].Parts;
            var secondParts = secondSegments[segment].Parts;
            if (firstParts.Count != secondParts.Count)
            {
                return false;
            }

            for (var part = 0; part < firstParts.Count; part++)
            {
                if (
                    firstParts[part] is RoutePatternLiteralPart firstLiteral
                    && secondParts[part] is RoutePatternLiteralPart secondLiteral
                    && !string.Equals(firstLiteral.Content, secondLiteral.Content, StringComparison.OrdinalIgnoreCase)
                )
                {
                    return false;
                }
            }
        }

        return true;
    }

    private sealed record ParsedRoute(Type Component, RoutePattern Pattern, decimal Precedence);
}
