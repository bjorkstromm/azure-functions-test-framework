using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace AzureFunctions.TestFramework.Core.Routing;

/// <summary>
/// Matches an HTTP method and request path against registered Azure Functions HTTP trigger routes,
/// supporting the full set of ASP.NET route constraints (int, guid, alpha, minlength, range, regex,
/// optional parameters, and catch-all segments).
/// </summary>
/// <remarks>
/// Uses ASP.NET Core's <see cref="TemplateMatcher"/> for path-structure matching and
/// <c>IInlineConstraintResolver</c> to resolve and evaluate the same
/// <c>IRouteConstraint</c> implementations that the real ASP.NET Core host uses.
/// Route priority follows the ASP.NET Web API 2 ordering rules:
/// literal segments beat constrained parameters, which beat unconstrained parameters,
/// which beat optional parameters, which beat catch-all segments.
/// </remarks>
public sealed class RouteMatcher
{
    // Shared, stateless constraint resolver — built once for the lifetime of the test process.
    private static readonly IInlineConstraintResolver SharedConstraintResolver = CreateConstraintResolver();

    // Sentinel used in the TemplateMatcher defaults dictionary to mark optional parameters.
    // When the path has no segment for an optional parameter, the matcher sets the value to
    // this sentinel instead of leaving it absent; we filter it out before returning route params.
    private static readonly object OptionalSentinel = new();

    private readonly List<RouteEntry> _routes = [];

    /// <summary>
    /// Registers an HTTP trigger route. Routes are sorted by specificity after each addition
    /// so that <see cref="Match"/> always returns the most specific match.
    /// </summary>
    /// <param name="httpMethod">The HTTP method (e.g. <c>"GET"</c>).</param>
    /// <param name="routeTemplate">
    /// The route template as declared in the function binding (e.g.
    /// <c>"products/{id:int}"</c>, <c>"files/{*rest}"</c>, <c>"items/{page?}"</c>).
    /// </param>
    /// <param name="functionId">The function ID to associate with this route.</param>
    public void AddRoute(string httpMethod, string routeTemplate, string functionId)
    {
        var template = TemplateParser.Parse(routeTemplate);
        var defaults = BuildDefaults(template);
        var matcher = new TemplateMatcher(template, defaults);
        var constraints = BuildConstraints(template);
        var priority = ComputePriority(template);

        _routes.Add(new RouteEntry(httpMethod.ToUpperInvariant(), functionId, matcher, constraints, priority));

        // Keep routes sorted: highest priority first so Match() returns the best match.
        _routes.Sort(static (a, b) => b.Priority.CompareTo(a.Priority));
    }

    /// <summary>
    /// Matches an HTTP method and a normalized path (route prefix already stripped, no leading
    /// slash, no query string) against the registered routes.
    /// </summary>
    /// <returns>
    /// The function ID of the best-matching route and a dictionary of extracted route parameter
    /// values, or <c>(null, empty)</c> when no route matches.
    /// </returns>
    public (string? FunctionId, IReadOnlyDictionary<string, string> RouteParams) Match(
        string httpMethod, string normalizedPath)
    {
        var method = httpMethod.ToUpperInvariant();

        // TemplateMatcher expects a PathString that starts with '/'.
        var pathString = new PathString("/" + normalizedPath.TrimStart('/'));

        foreach (var entry in _routes)
        {
            if (entry.Method != method) continue;

            var values = new RouteValueDictionary();
            if (!entry.Matcher.TryMatch(pathString, values)) continue;
            if (!ValidateConstraints(entry.Constraints, values)) continue;

            // Convert to string dictionary, skipping the OptionalSentinel values
            // (which indicate an optional parameter that was absent in the request path).
            var routeParams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, val) in values)
            {
                if (val is null || ReferenceEquals(val, OptionalSentinel)) continue;
                routeParams[key] = val.ToString()!;
            }

            return (entry.FunctionId, routeParams);
        }

        return (null, ReadOnlyDictionary<string, string>.Empty);
    }

    /// <summary>
    /// Builds the defaults dictionary required by <see cref="TemplateMatcher"/>: optional
    /// parameters are seeded with <see cref="OptionalSentinel"/> so the matcher knows they
    /// may be absent from the path, and we can detect absence in the result.
    /// </summary>
    private static RouteValueDictionary BuildDefaults(RouteTemplate template)
    {
        var defaults = new RouteValueDictionary();
        foreach (var parameter in template.Parameters)
        {
            if (parameter.Name is null) continue;
            if (parameter.IsOptional)
                defaults[parameter.Name] = OptionalSentinel;
            else if (parameter.DefaultValue is not null)
                defaults[parameter.Name] = parameter.DefaultValue;
        }
        return defaults;
    }

    /// <summary>
    /// Pre-resolves inline constraints (e.g. <c>int</c>, <c>guid</c>, <c>min(1)</c>) for each
    /// parameter in the template using <c>IInlineConstraintResolver</c>.
    /// </summary>
    private static Dictionary<string, List<IRouteConstraint>> BuildConstraints(RouteTemplate template)
    {
        var result = new Dictionary<string, List<IRouteConstraint>>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in template.Parameters)
        {
            if (parameter.Name is null) continue;
            var inlineConstraints = parameter.InlineConstraints;
            if (inlineConstraints is null) continue;

            var list = new List<IRouteConstraint>();
            foreach (var ic in inlineConstraints)
            {
                var constraint = SharedConstraintResolver.ResolveConstraint(ic.Constraint);
                if (constraint is not null) list.Add(constraint);
            }

            if (list.Count > 0) result[parameter.Name] = list;
        }
        return result;
    }

    /// <summary>
    /// Validates that all route parameter values satisfy their resolved constraints.
    /// </summary>
    private static bool ValidateConstraints(
        Dictionary<string, List<IRouteConstraint>> constraints,
        RouteValueDictionary values)
    {
        foreach (var (name, paramConstraints) in constraints)
        {
            if (!values.TryGetValue(name, out var value)) continue;

            foreach (var constraint in paramConstraints)
            {
                // httpContext and route are not used by any built-in ASP.NET Core constraint
                // (int, guid, alpha, minlength, range, regex, …), so null is safe here.
                if (!constraint.Match(null, null, name, values, RouteDirection.IncomingRequest))
                    return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Computes a numeric specificity score for a route template so that more-specific routes
    /// are tried before less-specific ones. Follows ASP.NET Web API 2 route ordering:
    /// literal &gt; constrained param &gt; unconstrained param &gt; optional param &gt; catch-all.
    /// </summary>
    private static int ComputePriority(RouteTemplate template)
    {
        var score = 0;
        foreach (var segment in template.Segments)
        {
            foreach (var part in segment.Parts)
            {
                if (part.IsLiteral)
                {
                    score += 100;
                }
                else if (part.IsParameter)
                {
                    var hasConstraints = part.InlineConstraints?.Any() == true;
                    if (part.IsCatchAll)
                        score += 1;
                    else if (part.IsOptional)
                        score += hasConstraints ? 5 : 2;
                    else
                        score += hasConstraints ? 20 : 10;
                }
            }
        }
        return score;
    }

    private static IInlineConstraintResolver CreateConstraintResolver()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRouting();
        return services.BuildServiceProvider()
            .GetRequiredService<IInlineConstraintResolver>();
    }

    private sealed record RouteEntry(
        string Method,
        string FunctionId,
        TemplateMatcher Matcher,
        Dictionary<string, List<IRouteConstraint>> Constraints,
        int Priority);
}
