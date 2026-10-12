namespace PMPlatform.Application.Features.AuditActivity;

/// <summary>
/// The SERVICE principal <c>svc.audit-capture</c> (db/seed): the author of an event whose actor is not known — a refused anonymous
/// sign-in, a token that failed validation (ERD D-2: <c>created_by</c> is never empty) — and of every row AuditActivity writes for
/// itself: ingestion, SIEM forwarding, the Activity projection, and an operation run from the command line.
/// </summary>
public static class AuditCapturePrincipal
{
    public static readonly Guid Id = new("00000000-0000-4000-8000-0000000000fd");
}
