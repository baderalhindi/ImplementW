namespace PMPlatform.Application.Common.Observability;

/// <summary>What a <see cref="RedactedField"/> protects.</summary>
public enum RedactedFieldCategory
{
    /// <summary>Something that grants access: a password, token, key, secret or second-factor code.</summary>
    Credential,

    /// <summary>A value that identifies a person: a national ID or Iqama number, a name.</summary>
    PersonalIdentifier,

    /// <summary>A way to reach a person: an e-mail address or a mobile number.</summary>
    ContactDetail,

    /// <summary>Where a request came from: the client's IP address.</summary>
    NetworkIdentity,
}

/// <summary>
/// One entry of the redaction registry (<see cref="RedactionRegistry"/>). A field is found two ways. By <see cref="Names"/>: the
/// names it travels under (a header, query parameter, JSON property, log property, environment variable, or <c>name=value</c> in
/// text). A name matches when, with case and the separators <c>-</c>, <c>_</c>, <c>.</c> and space ignored, it is one of these
/// or ends with one, so <c>password</c> also covers <c>newPassword</c> and <c>AD_BIND_PASSWORD</c>. By
/// <see cref="ValuePattern"/>: where the value has a shape of its own, it is found in free text whatever it is called.
/// </summary>
/// <param name="Field">The field, as the policy names it.</param>
/// <param name="Category">What it protects.</param>
/// <param name="Names">The names it travels under, written in lower case with no separators.</param>
/// <param name="ValuePattern">A regular expression matching the value itself in free text, or null when the value has no recognisable shape.</param>
/// <param name="Source">The requirement that puts the field on the registry.</param>
public sealed record RedactedField(
    string Field,
    RedactedFieldCategory Category,
    IReadOnlyList<string> Names,
    string? ValuePattern,
    string Source);
