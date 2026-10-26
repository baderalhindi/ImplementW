using System.Diagnostics.CodeAnalysis;

namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>The value of an administration request, or why it was refused. Exactly one of the two is set.</summary>
public sealed class AdministrationResult<T>
    where T : class
{
    private AdministrationResult(T? value, AdministrationError? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }

    public AdministrationError? Error { get; }

    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool Succeeded => Error is null;

    public static implicit operator AdministrationResult<T>(T value) => new(value ?? throw new ArgumentNullException(nameof(value)), null);

    public static implicit operator AdministrationResult<T>(AdministrationError error) => new(null, error ?? throw new ArgumentNullException(nameof(error)));
}
