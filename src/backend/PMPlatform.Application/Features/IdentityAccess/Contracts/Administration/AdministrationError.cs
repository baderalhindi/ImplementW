namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>A refused administration request. <see cref="Code"/> is set for the module codes of 409 and 422 (R-27).</summary>
public sealed record AdministrationError(AdministrationErrorKind Kind, string? Code, IReadOnlyList<FieldIssue> Fields)
{
    public static AdministrationError NotFound { get; } = new(AdministrationErrorKind.NotFound, null, []);

    public static AdministrationError Forbidden { get; } = new(AdministrationErrorKind.Forbidden, null, []);

    public static AdministrationError InvalidTransition { get; } = new(AdministrationErrorKind.InvalidTransition, null, []);

    public static AdministrationError TerminalState { get; } = new(AdministrationErrorKind.TerminalState, null, []);

    public static AdministrationError PreconditionFailed { get; } = new(AdministrationErrorKind.PreconditionFailed, null, []);

    public static AdministrationError Unavailable { get; } = new(AdministrationErrorKind.Unavailable, null, []);

    public static AdministrationError Rule(string code, params FieldIssue[] fields) => new(AdministrationErrorKind.RuleViolated, code, fields);

    public static AdministrationError Conflict(string code, params FieldIssue[] fields) => new(AdministrationErrorKind.Conflict, code, fields);

    public static AdministrationError Duplicate(string field) => Conflict(IdentityAccessErrorCodes.DuplicateKey, new FieldIssue(field, FieldIssue.Duplicate));
}
