namespace PMPlatform.Application.Features.DocumentManagement.Contracts;

/// <summary>
/// A record of another module that a document is linked to, by identity only (M-4): the owning module's ADR-003 name,
/// the record type and its id. DocumentManagement never reads it.
/// </summary>
public sealed record BusinessTarget(string Module, string Type, Guid Id)
{
    public const int ModuleLength = 50;
    public const int TypeLength = 100;

    /// <summary>A malformed target is the calling module's programming error, not a request to refuse.</summary>
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Module);
        ArgumentException.ThrowIfNullOrWhiteSpace(Type);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(Module.Length, ModuleLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(Type.Length, TypeLength);
        if (Id == Guid.Empty)
        {
            throw new ArgumentException("The target id is empty.", nameof(Id));
        }
    }
}
