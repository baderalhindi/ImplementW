using System.Collections;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Validation;

namespace PMPlatform.Tests.Integration.InputValidation;

/// <summary>
/// TASK-077: <see cref="RequestTextValidation"/> sees every string an action receives only if every argument is something it walks —
/// a string, a scalar, a collection or dictionary of those, or one of the platform's own models. An argument of any other type
/// (a <c>JsonElement</c>, an <c>object</c>, a third-party type) would carry text past it; this fails until the walk is taught it.
/// </summary>
public sealed class RequestTextCoverageTests
{
    private static readonly HashSet<Type> Scalars =
    [
        typeof(string), typeof(decimal), typeof(Guid), typeof(DateOnly), typeof(TimeOnly), typeof(DateTime), typeof(DateTimeOffset),
        typeof(TimeSpan), typeof(CancellationToken),
    ];

    [Fact]
    public void EveryActionArgumentIsOneTheTextRulesWalk()
    {
        List<string> unwalked = [];
        Type[] controllers = [.. typeof(Program).Assembly.GetTypes().Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract)];
        foreach (MethodInfo action in controllers.SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)))
        {
            foreach (ParameterInfo parameter in action.GetParameters())
            {
                Check(parameter.ParameterType, $"{action.DeclaringType!.Name}.{action.Name}({parameter.Name})", unwalked, []);
            }
        }

        Assert.NotEmpty(controllers);
        Assert.Empty(unwalked);
    }

    private static void Check(Type declared, string path, List<string> unwalked, HashSet<Type> seen)
    {
        Type type = Nullable.GetUnderlyingType(declared) ?? declared;
        if (type.IsPrimitive || type.IsEnum || Scalars.Contains(type) || !seen.Add(type))
        {
            return;
        }

        if (RequestTextValidation.IsModel(type))
        {
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.GetIndexParameters().Length == 0))
            {
                Check(property.PropertyType, $"{path}.{property.Name}", unwalked, seen);
            }
        }
        else if (type.IsArray)
        {
            Check(type.GetElementType()!, $"{path}[]", unwalked, seen);
        }
        else if (type.IsGenericType && typeof(IEnumerable).IsAssignableFrom(type))
        {
            foreach (Type argument in type.GetGenericArguments())
            {
                Check(argument, $"{path}<>", unwalked, seen);
            }
        }
        else
        {
            unwalked.Add($"{path}: {type.FullName}");
        }
    }
}
