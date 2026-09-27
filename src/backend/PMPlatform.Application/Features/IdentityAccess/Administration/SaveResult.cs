using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.IdentityAccess.Administration;

/// <summary>What saving the tracked changes came to. <see cref="DuplicateField"/> is the request field of the violated key.</summary>
public sealed record SaveResult(SaveOutcome Outcome, string? DuplicateField = null)
{
    public static SaveResult Saved { get; } = new(SaveOutcome.Saved);

    public static SaveResult ConcurrencyConflict { get; } = new(SaveOutcome.ConcurrencyConflict);

    /// <summary>Null when saved; otherwise the refusal the API answers.</summary>
    public AdministrationError? Error => Outcome switch
    {
        SaveOutcome.Saved => null,
        SaveOutcome.ConcurrencyConflict => AdministrationError.PreconditionFailed,
        SaveOutcome.DuplicateKey => AdministrationError.Duplicate(DuplicateField ?? "body"),
        _ => throw new InvalidOperationException($"Unknown save outcome {Outcome}."),
    };
}
