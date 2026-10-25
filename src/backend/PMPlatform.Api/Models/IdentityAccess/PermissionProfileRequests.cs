using System.Globalization;
using System.Text.Json;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.MasterDataConfig;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Api.Models.IdentityAccess;

/// <summary>ADM-008 (TASK-110): a new profile on a canonical role, its first DRAFT copying <c>sourceVersionId</c>'s grants when given.</summary>
public sealed record PermissionProfileCreateRequest(string? Code, BilingualLabelRequest? Name, Guid? BaseRoleId, Guid? SourceVersionId)
{
    internal List<FieldError> Validate(out PermissionProfileCreateDraft? draft)
    {
        List<FieldError> errors = [];
        RequestValidation.Code(Code, "code", errors);
        RequestValidation.Label(Name, "name", errors);
        RequestValidation.RequireId(BaseRoleId, "baseRoleId", errors);
        draft = errors.Count > 0 ? null : new PermissionProfileCreateDraft(Code!, Name!.ToLabel(), BaseRoleId!.Value, SourceVersionId);
        return errors;
    }
}

/// <summary>ADM-008 (TASK-110): a new DRAFT of a profile, copying <c>sourceVersionId</c>, or the newest PUBLISHED version when absent.</summary>
public sealed record PermissionProfileVersionCreateRequest(Guid? SourceVersionId);

/// <summary>One cell of the grid: a permission and the scope it is held at (R-19 values, e.g. <c>READ_ONLY</c>).</summary>
public sealed record PermissionGrantRequest(string? PermissionCode, string? DataScope);

/// <summary>ADM-008 (TASK-110): a DRAFT's whole grid and its change summary. A permission left out is not held.</summary>
public sealed record PermissionGrantSetRequest(IReadOnlyList<PermissionGrantRequest>? Grants, NarrativeTextRequest? ChangeSummary)
{
    /// <summary>More than the catalogue holds; a longer grid is not a grid.</summary>
    public const int MaxGrants = 200;

    private static readonly Dictionary<string, DataScope> Scopes = Enum.GetValues<DataScope>()
        .ToDictionary(s => JsonNamingPolicy.SnakeCaseUpper.ConvertName(s.ToString()), StringComparer.Ordinal);

    internal List<FieldError> Validate(out PermissionGrantSetChange? change)
    {
        List<FieldError> errors = [];
        List<PermissionGrantDraft> grants = [];
        if (Grants is null)
        {
            errors.Add(new FieldError("grants", FieldError.Required));
        }
        else if (Grants.Count > MaxGrants)
        {
            errors.Add(new FieldError("grants", FieldError.MaxLength));
        }
        else
        {
            for (int i = 0; i < Grants.Count; i++)
            {
                string at = $"grants[{i.ToString(CultureInfo.InvariantCulture)}]";
                RequestValidation.Require(Grants[i]?.PermissionCode, $"{at}.permissionCode", RequestValidation.CodeLength, errors);
                string? scope = Grants[i]?.DataScope;
                if (scope is null || !Scopes.TryGetValue(scope, out DataScope parsed))
                {
                    errors.Add(new FieldError($"{at}.dataScope", scope is null ? FieldError.Required : FieldError.EnumValue));
                    continue;
                }

                grants.Add(new PermissionGrantDraft(Grants[i]!.PermissionCode!, parsed));
            }
        }

        Domain.Common.NarrativeText? summary = ChangeSummary?.Validate("changeSummary", errors);
        change = errors.Count > 0 ? null : new PermissionGrantSetChange(grants, summary);
        return errors;
    }
}
