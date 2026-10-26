using System.Text.RegularExpressions;
using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.AuditActivity.Contracts;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.AuditActivity;

/// <summary>
/// What makes a captured audit event trustworthy evidence (FG-06 §4.2 VALIDATED, §25.2): checked when the event is captured,
/// so a malformed event fails its producer's action, and again when it is ingested, so a message that did not come through the
/// trail is never stored. Secrets are removed before anything is persisted (AUD-CC-14): an attribute whose name says it holds a
/// secret, or whose value has the shape of a bearer token, a JWT or a private key, keeps its name and loses its value.
/// </summary>
public static partial class AuditCaptureRules
{
    /// <summary>What a removed secret is replaced by.</summary>
    public const string Redacted = "[REDACTED]";

    /// <summary>Name fragments that mark a secret by what it is (FG-06 §5.5): never a code, an id or a reason.</summary>
    private static readonly string[] SecretNameParts = ["password", "passphrase", "secret", "token", "credential", "private_key", "api_key", "authorization_header", "cookie"];

    /// <summary>The attributes with every secret value replaced by <see cref="Redacted"/>.</summary>
    public static IReadOnlyList<AuditAttribute> Redact(IEnumerable<AuditAttribute> attributes)
    {
        ArgumentNullException.ThrowIfNull(attributes);
        return [.. attributes.Select(a => IsSecretName(a.Name)
            ? new AuditAttribute(a.Name, RedactedOrNull(a.OldValue), RedactedOrNull(a.NewValue))
            : new AuditAttribute(a.Name, RedactValue(a.OldValue), RedactValue(a.NewValue)))];
    }

    /// <summary>
    /// The FG-06 §22 codes of everything wrong with the capture, in a stable order; empty when it may be stored. A secret is not
    /// a problem here: <see cref="Redact"/> has already removed it.
    /// </summary>
    public static IReadOnlyList<string> Problems(AuditCapture capture)
    {
        ArgumentNullException.ThrowIfNull(capture);
        List<string> problems = [];
        if (capture.SchemaVersion != AuditCapture.CurrentSchemaVersion)
        {
            problems.Add(AuditActivityErrorCodes.SchemaUnsupported);
        }

        if (capture.EventId == Guid.Empty || capture.CorrelationId == Guid.Empty || capture.OccurredAt == default
            || !Enum.IsDefined(capture.EventClass) || !Enum.IsDefined(capture.Outcome) || !Enum.IsDefined(capture.ActorType))
        {
            problems.Add(AuditActivityErrorCodes.EventInvalid);
        }

        if (capture.EventType is null || capture.EventType.Length > 100 || !EventTypePattern().IsMatch(capture.EventType))
        {
            problems.Add(AuditActivityErrorCodes.EventTypeUnsupported);
        }

        // A service principal is a user (ERD D-2): a SERVICE event names it. Only an unidentified person or an integration has no actor.
        if (capture.ActorType == AuditActorType.Service && capture.ActorUserId is null)
        {
            problems.Add(AuditActivityErrorCodes.ActorInvalid);
        }

        if (capture.Subject is { } subject
            && (string.IsNullOrEmpty(subject.Module) || subject.Module.Length > 50 || string.IsNullOrEmpty(subject.Type) || subject.Type.Length > 100 || subject.Id == Guid.Empty))
        {
            problems.Add(AuditActivityErrorCodes.SourceReferenceInvalid);
        }

        if (capture.Attributes is null
            || capture.Attributes.Any(a => a is null || a.Name is null || a.Name.Length > 100 || !AttributeNamePattern().IsMatch(a.Name))
            || capture.Attributes.Select(a => a?.Name).Distinct(StringComparer.Ordinal).Count() != capture.Attributes.Count)
        {
            problems.Add(AuditActivityErrorCodes.ChangeSetInvalid);
        }

        return problems;
    }

    private static bool IsSecretName(string? name) =>
        name is not null && SecretNameParts.Any(part => name.Contains(part, StringComparison.OrdinalIgnoreCase));

    private static string? RedactedOrNull(string? value) => value is null ? null : Redacted;

    private static string? RedactValue(string? value) => value is not null && SecretValuePattern().IsMatch(value) ? Redacted : value;

    /// <summary>EV-1: <c>&lt;ProducerModule&gt;.&lt;EventName&gt;</c>, both PascalCase.</summary>
    [GeneratedRegex(@"^[A-Z][A-Za-z0-9]*\.[A-Z][A-Za-z0-9]*$", RegexOptions.CultureInvariant)]
    private static partial Regex EventTypePattern();

    /// <summary>An identifier: letters, digits and underscores, starting with a letter. Most are snake case; FG-04 writes <c>riskRatings_count</c>.</summary>
    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex AttributeNamePattern();

    /// <summary>An HTTP authorization value, a JWT, or a PEM private key, wherever it appears in the value.</summary>
    [GeneratedRegex(@"(^\s*(Bearer|Basic)\s+\S)|(eyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.)|(-----BEGIN [A-Z ]*PRIVATE KEY-----)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex SecretValuePattern();
}
