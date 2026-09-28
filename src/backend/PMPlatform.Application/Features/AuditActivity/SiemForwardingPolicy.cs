using PMPlatform.Application.Common.Auditing;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.AuditActivity;

/// <summary>
/// Configuration section <c>Audit:Siem</c>: which audit classes are forwarded to AHDA's SIEM (PTBC-029, CTL-26). The
/// subset is AHDA Cybersecurity's to set, so it is configuration over events that are stored whatever it says.
/// Authentication is always forwarded: CTL-26 requires at least the authentication-failure class end to end.
/// </summary>
public sealed class SiemForwardingPolicy
{
    public const string Section = "Audit:Siem";

    /// <summary>The class the policy may not leave out (CTL-26).</summary>
    public static readonly string MinimumForwardedClass = AuditValue.Format(AuditEventClass.Authentication)!;

    /// <summary>ERD <c>event_class</c> values, e.g. <c>AUTHENTICATION</c>, <c>AUTHORIZATION_DENIAL</c>.</summary>
    public IList<string> ForwardedClasses { get; } = [];

    public bool Forwards(AuditEventClass eventClass) => ForwardedClasses.Contains(AuditValue.Format(eventClass)!, StringComparer.Ordinal);

    /// <summary>Entries that name no audit class; a typo would otherwise silently forward nothing for it.</summary>
    public IEnumerable<string> UnknownClasses() =>
        ForwardedClasses.Except(Enum.GetValues<AuditEventClass>().Select(c => AuditValue.Format(c)!), StringComparer.Ordinal);
}
