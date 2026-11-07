using System.Text.RegularExpressions;
using PMPlatform.Api.Errors;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Dashboards;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Api.Models.IdentityAccess;

/// <summary>New Role (the 5 Nov 2026 review §6.2).</summary>
public sealed partial record RoleCreateRequest(
    string? Code,
    string? NameAr,
    string? NameEn,
    bool? IsExternalEligible,
    IReadOnlyList<string>? AllowedScopes,
    Guid? SourceRoleId = null,
    string? LandingDashboardCode = null)
{
    internal List<FieldError> Validate(out RoleCreateDraft? draft)
    {
        List<FieldError> errors = [];
        RequestValidation.RequireText(Code, "code", 40, errors);
        if (Code is not null && !CodePattern().IsMatch(Code))
        {
            errors.Add(new FieldError("code", FieldError.Malformed));
        }

        RequestValidation.RequireText(NameAr, "nameAr", RequestValidation.NameLength, errors);
        RequestValidation.RequireText(NameEn, "nameEn", RequestValidation.NameLength, errors);
        RoleSettingsRequest.ValidateSettings(AllowedScopes, LandingDashboardCode, errors);
        draft = errors.Count > 0
            ? null
            : new RoleCreateDraft(Code!, new BilingualLabel(NameAr!, NameEn!), IsExternalEligible ?? false, AllowedScopes!, SourceRoleId, LandingDashboardCode);
        return errors;
    }

    [GeneratedRegex("^[A-Z][A-Z0-9_-]{1,39}$", RegexOptions.CultureInvariant)]
    private static partial Regex CodePattern();
}

/// <summary>A role's allowed scopes and landing dashboard.</summary>
public sealed record RoleSettingsRequest(IReadOnlyList<string>? AllowedScopes, string? LandingDashboardCode = null, bool? ShowCreateInHeader = null)
{
    internal List<FieldError> Validate()
    {
        List<FieldError> errors = [];
        ValidateSettings(AllowedScopes, LandingDashboardCode, errors);
        return errors;
    }

    internal static void ValidateSettings(IReadOnlyList<string>? allowedScopes, string? landingDashboardCode, List<FieldError> errors)
    {
        if (allowedScopes is null || allowedScopes.Count == 0)
        {
            errors.Add(new FieldError("allowedScopes", FieldError.Required));
        }
        else if (allowedScopes.Any(s => !RoleScopes.Known.Contains(s, StringComparer.Ordinal)))
        {
            errors.Add(new FieldError("allowedScopes", FieldError.EnumValue));
        }

        if (landingDashboardCode is not null && !Enum.GetNames<DashboardCode>().Contains(landingDashboardCode, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add(new FieldError("landingDashboardCode", FieldError.EnumValue));
        }
    }
}
