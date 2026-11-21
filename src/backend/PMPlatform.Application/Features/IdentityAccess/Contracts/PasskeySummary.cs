namespace PMPlatform.Application.Features.IdentityAccess.Contracts;

/// <summary>One of the signed-in person's passkeys, as their account page lists it. The key material is never shown.</summary>
public sealed record PasskeySummary(Guid Id, string Name, DateTimeOffset CreatedAt, DateTimeOffset? LastUsedAt)
{
    /// <summary>The longest device name kept.</summary>
    public const int NameLength = 100;
}

/// <summary>The signed-in person's passkeys, newest first.</summary>
public sealed record PasskeyList(IReadOnlyList<PasskeySummary> Items);
