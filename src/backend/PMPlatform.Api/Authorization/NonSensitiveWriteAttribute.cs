namespace PMPlatform.Api.Authorization;

/// <summary>
/// api-conventions R-35: a <c>POST</c> or <c>PUT</c> that is not a sensitive write, with the reason, so it needs no
/// <c>Idempotency-Key</c>. Only the classes R-35 enumerates qualify — user preferences, marking a notification read, and
/// the like; anything else is a <see cref="SensitiveWriteAttribute"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class NonSensitiveWriteAttribute(string reason) : Attribute
{
    public string Reason { get; } = reason;
}
