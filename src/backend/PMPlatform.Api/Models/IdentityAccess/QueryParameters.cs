using System.Text.Json;
using PMPlatform.Api.Errors;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Api.Models.IdentityAccess;

/// <summary>Collection query parameters (api-conventions R-29, R-31, R-32).</summary>
internal static class QueryParameters
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 200;

    /// <summary>R-29: <c>page</c> from 1, <c>pageSize</c> 1 to 200, default 1 and 25.</summary>
    public static PageRequest Page(int? page, int? pageSize, List<FieldError> errors)
    {
        if (page is < 1)
        {
            errors.Add(new FieldError("page", FieldError.OutOfRange));
        }

        if (pageSize is < 1 or > MaxPageSize)
        {
            errors.Add(new FieldError("pageSize", FieldError.OutOfRange));
        }

        return new PageRequest(Math.Max(page ?? 1, 1), Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize));
    }

    /// <summary>R-31: a comma-separated set of R-19 values, e.g. <c>status=ACTIVE,DISABLED</c>. Absent: the empty set, no filter.</summary>
    public static IReadOnlyCollection<TEnum> EnumSet<TEnum>(string? value, string field, List<FieldError> errors)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrEmpty(value))
        {
            return [];
        }

        Dictionary<string, TEnum> names = Enum.GetValues<TEnum>().ToDictionary(v => JsonNamingPolicy.SnakeCaseUpper.ConvertName(v.ToString()), StringComparer.Ordinal);
        HashSet<TEnum> values = [];
        foreach (string name in value.Split(','))
        {
            if (names.TryGetValue(name, out TEnum parsed))
            {
                values.Add(parsed);
            }
            else
            {
                errors.Add(new FieldError(field, FieldError.EnumValue));
                return [];
            }
        }

        return values;
    }

    /// <summary>R-32 for ADM-002: <c>displayName</c> (the default) or <c>username</c>, <c>:asc</c> or <c>:desc</c>.</summary>
    public static UserSort UserSortOf(string? value, List<FieldError> errors)
    {
        switch (value)
        {
            case null or "displayName:asc":
                return UserSort.DisplayNameAscending;
            case "displayName:desc":
                return UserSort.DisplayNameDescending;
            case "username:asc":
                return UserSort.UsernameAscending;
            case "username:desc":
                return UserSort.UsernameDescending;
            default:
                errors.Add(new FieldError("sort", FieldError.EnumValue));
                return UserSort.DisplayNameAscending;
        }
    }
}
