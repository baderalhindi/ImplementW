using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Validation;

namespace PMPlatform.Tests.Integration.InputValidation;

/// <summary>
/// TASK-077: how <see cref="RequestTextValidation"/> walks what an action receives — nested models, lists, dictionaries, renamed
/// properties and plain parameters — and names each field it refuses as the request names it (R-23), without a host.
/// </summary>
public sealed class RequestTextWalkTests
{
    private const string Bell = "\u0007";

    [Fact]
    public void EveryStringInABodyIsReachedAndNamedByItsPathIntoTheBody()
    {
        Order body = new(
            Title: new Text("fine"),
            Lines: [new Line("ok", ["ok", Bell]), new Line(Bell, [])],
            Labels: new Dictionary<string, string> { ["en"] = Bell, [Bell] = "ok" },
            Renamed: Bell);

        Assert.Equal(
            ["lines[0].tags[1] INVALID_CHARACTER", "lines[1].name INVALID_CHARACTER", "labels.en INVALID_CHARACTER", "labels INVALID_CHARACTER", "ref INVALID_CHARACTER"],
            Refusals(("order", typeof(Order), BindingSource.Body, body)));
    }

    [Fact]
    public void AParameterIsNamedByItselfAndAModelFromTheQueryByItsProperties()
    {
        Assert.Equal(
            ["q INVALID_CHARACTER", "status[1] MAX_LENGTH", "name INVALID_CHARACTER"],
            Refusals(
                ("q", typeof(string), BindingSource.Query, Bell),
                ("status", typeof(string[]), BindingSource.Query, new[] { "OPEN", new string('A', 10_001) }),
                ("filter", typeof(Line), BindingSource.Query, new Line(Bell, [])),
                ("page", typeof(int?), BindingSource.Query, 2)));
    }

    [Fact]
    public void ARequestThatObeysTheRulesReachesItsAction()
    {
        Order body = new(new Text("خطر\tline\nnext"), [new Line("<b>x</b>", ["'; DROP TABLE x;--"])], new Dictionary<string, string> { ["ar"] = "عربي" }, null);
        Assert.Empty(Refusals(("order", typeof(Order), BindingSource.Body, body), ("q", typeof(string), BindingSource.Query, null)));
    }

    private static string[] Refusals(params (string Name, Type Type, BindingSource Source, object? Value)[] arguments)
    {
        ServiceCollection services = new();
        services.AddSingleton(TimeProvider.System);
        DefaultHttpContext http = new() { RequestServices = services.BuildServiceProvider() };
        ActionDescriptor action = new()
        {
            Parameters = [.. arguments.Select(a => new ParameterDescriptor { Name = a.Name, ParameterType = a.Type, BindingInfo = new BindingInfo { BindingSource = a.Source } })],
        };
        ActionExecutingContext context = new(
            new ActionContext(http, new RouteData(), action), [], arguments.ToDictionary(a => a.Name, a => a.Value), controller: new object());

        new RequestTextValidation().OnActionExecuting(context);

        return context.Result is ObjectResult { Value: ProblemDetails problem }
            ? [.. ((IReadOnlyList<FieldError>)problem.Extensions["errors"]!).Select(e => $"{e.Field} {e.Code}")]
            : [];
    }

    public sealed record Text(string? Value);

    public sealed record Line(string? Name, IReadOnlyList<string> Tags);

    public sealed record Order(Text? Title, IReadOnlyList<Line> Lines, IReadOnlyDictionary<string, string> Labels, [property: JsonPropertyName("ref")] string? Renamed);
}
