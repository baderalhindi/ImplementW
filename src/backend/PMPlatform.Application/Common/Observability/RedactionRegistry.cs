namespace PMPlatform.Application.Common.Observability;

/// <summary>
/// The redaction registry (CTL-27): every field that never leaves the platform in an error report or APM payload, explicitly.
/// <see cref="TelemetryRedactor"/> applies it to everything the APM integration sends (TASK-090), and the cross-check
/// (<c>ApmRedactionTests</c>) submits a marker value for each entry and proves the marker is absent from what was sent.
/// </summary>
/// <remarks>
/// Adding a field here is the whole change: it is redacted, and cross-checked, from then on. The classification taxonomy that
/// will mark further fields sensitive is outstanding with AHDA Cybersecurity (UGV-01, ADR-010); until it lands the registry holds
/// the fields CTL-27 names and those earlier records withhold. The registry governs telemetry and error reports; it is never
/// applied to the Formal Audit store's governed content (CTL-27, Blueprint Section 18).
/// </remarks>
public static class RedactionRegistry
{
    private const string Ctl27 = "CTL-27: passwords, tokens and secrets are stripped from logs, error reports and APM payloads";

    /// <summary>The registered fields.</summary>
    public static IReadOnlyList<RedactedField> Fields { get; } =
    [
        new("Password", RedactedFieldCategory.Credential, ["password", "passwd"], null, Ctl27),
        new("Secret, client secret, API key", RedactedFieldCategory.Credential, ["secret", "apikey", "accesskey", "privatekey", "signingkey"], null, Ctl27),
        new(
            "Token (access, refresh, MFA, verification; any JWT)",
            RedactedFieldCategory.Credential,
            ["token"],
            @"eyJ[A-Za-z0-9_-]{4,}\.[A-Za-z0-9_-]{4,}\.[A-Za-z0-9_-]*",
            Ctl27),
        new(
            "Authorization header",
            RedactedFieldCategory.Credential,
            ["authorization"],
            @"\b(?:Bearer|Basic|Negotiate)\s+[A-Za-z0-9._~+/=-]+",
            Ctl27),
        new("Cookie", RedactedFieldCategory.Credential, ["cookie", "cookies"], null, Ctl27),
        new("Connection string", RedactedFieldCategory.Credential, ["connectionstring"], null, Ctl27),
        new(
            "Credentials in a URL, APM DSN",
            RedactedFieldCategory.Credential,
            ["dsn"],
            @"(?<=[a-z][a-z0-9+.-]*://)[^\s/?#@]+(?=@)",
            Ctl27),
        new(
            "Second-factor code",
            RedactedFieldCategory.Credential,
            ["otp", "mfacode", "verificationcode", "onetimecode", "onetimepassword"],
            null,
            "CTL-27; auth-access-audit-logging.md D-8 (never a second-factor code)"),
        new(
            "National ID or Iqama number",
            RedactedFieldCategory.PersonalIdentifier,
            ["nationalid", "nationalidnumber", "iqama", "iqamanumber", "identitynumber"],
            "(?<![0-9A-Za-z])[12][0-9]{9}(?![0-9A-Za-z])",
            "CTL-27: national ID numbers; nafath-data-minimisation.md"),
        new(
            "Person's name",
            RedactedFieldCategory.PersonalIdentifier,
            ["displayname", "fullname", "firstname", "lastname", "givenname", "surname"],
            null,
            "CTL-27: personal data, pending the UGV-01 taxonomy"),
        new(
            "E-mail address",
            RedactedFieldCategory.ContactDetail,
            ["email", "emailaddress"],
            @"[A-Za-z0-9._%+-]+@[A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)*\.[A-Za-z]{2,}",
            "auth-access-audit-logging.md D-8 (contact fields withheld)"),
        new(
            "Mobile number",
            RedactedFieldCategory.ContactDetail,
            ["mobile", "mobilenumber", "phone", "phonenumber", "msisdn"],
            @"(?<![0-9A-Za-z+])(?:\+966|00966|966|0)5[0-9]{8}(?![0-9A-Za-z])",
            "auth-access-audit-logging.md D-8; threat-model-sms-delivery-receipt.md R-7"),
        new(
            "Client IP address",
            RedactedFieldCategory.NetworkIdentity,
            ["clientaddress", "ipaddress", "remoteaddr", "xforwardedfor", "xrealip"],
            null,
            "CTL-27: personal data (the client address identifies a person's device)"),
    ];
}
