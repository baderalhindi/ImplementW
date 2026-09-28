using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;

namespace PMPlatform.Application.Features.MasterDataConfig;

/// <summary>What saving the tracked changes came to. <see cref="DuplicateField"/> is the request field of the violated key.</summary>
public sealed record SaveResult(SaveOutcome Outcome, string? DuplicateField = null)
{
    public static SaveResult Saved { get; } = new(SaveOutcome.Saved);

    public static SaveResult ConcurrencyConflict { get; } = new(SaveOutcome.ConcurrencyConflict);

    public static SaveResult EffectiveFromOutOfOrder { get; } = new(SaveOutcome.EffectiveFromOutOfOrder);

    /// <summary>Null when saved; otherwise the refusal the API answers.</summary>
    public AdministrationError? Error => Outcome switch
    {
        SaveOutcome.Saved => null,
        SaveOutcome.ConcurrencyConflict => AdministrationError.PreconditionFailed,
        SaveOutcome.DuplicateKey => AdministrationError.Conflict(
            MasterDataConfigErrorCodes.DuplicateKey, new FieldIssue(DuplicateField ?? "body", FieldIssue.Duplicate)),
        SaveOutcome.EffectiveFromOutOfOrder => AdministrationError.Rule(
            MasterDataConfigErrorCodes.EffectiveFromInvalid, new FieldIssue("effectiveFrom", FieldIssue.NotAllowed)),
        _ => throw new InvalidOperationException($"Unknown save outcome {Outcome}."),
    };
}
