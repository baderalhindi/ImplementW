using System.Text.Json;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.MasterDataConfig;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Api.Models.IdentityAccess;

/// <summary>Grants a permission at <c>dataScope</c>, or removes it (<c>GRANT</c>, <c>REMOVE</c>), for one user. <c>reason</c> is required.</summary>
public sealed record UserPermissionOverrideRequest(string? Effect, string? DataScope, NarrativeTextRequest? Reason)
{
    private static readonly Dictionary<string, PermissionOverrideEffect> Effects = Enum.GetValues<PermissionOverrideEffect>()
        .ToDictionary(e => JsonNamingPolicy.SnakeCaseUpper.ConvertName(e.ToString()), StringComparer.Ordinal);

    private static readonly Dictionary<string, DataScope> Scopes = Enum.GetValues<DataScope>()
        .ToDictionary(s => JsonNamingPolicy.SnakeCaseUpper.ConvertName(s.ToString()), StringComparer.Ordinal);

    internal List<FieldError> Validate(out UserPermissionOverrideChange? change)
    {
        List<FieldError> errors = [];
        change = null;
        PermissionOverrideEffect effect = default;
        if (Effect is null || !Effects.TryGetValue(Effect, out effect))
        {
            errors.Add(new FieldError("effect", Effect is null ? FieldError.Required : FieldError.EnumValue));
        }

        DataScope? scope = null;
        if (effect == PermissionOverrideEffect.Grant)
        {
            if (DataScope is null || !Scopes.TryGetValue(DataScope, out DataScope parsed))
            {
                errors.Add(new FieldError("dataScope", DataScope is null ? FieldError.Required : FieldError.EnumValue));
            }
            else
            {
                scope = parsed;
            }
        }

        NarrativeText? reason = UserPermissionOverrideReason.Validate(Reason, errors);
        if (errors.Count == 0)
        {
            change = new UserPermissionOverrideChange(effect, scope, reason!);
        }

        return errors;
    }
}

/// <summary>Puts a permission back to what the user's roles give. <c>reason</c> is required.</summary>
public sealed record UserPermissionOverrideClearRequest(NarrativeTextRequest? Reason)
{
    internal List<FieldError> Validate(out NarrativeText? reason)
    {
        List<FieldError> errors = [];
        reason = UserPermissionOverrideReason.Validate(Reason, errors);
        return errors;
    }
}

internal static class UserPermissionOverrideReason
{
    public static NarrativeText? Validate(NarrativeTextRequest? reason, List<FieldError> errors)
    {
        if (reason is null)
        {
            errors.Add(new FieldError("reason", FieldError.Required));
            return null;
        }

        return reason.Validate("reason", errors);
    }
}
