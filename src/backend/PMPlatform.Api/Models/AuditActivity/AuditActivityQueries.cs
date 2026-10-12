using System.Text.RegularExpressions;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Features.AuditActivity.Contracts;
using PMPlatform.Domain.Common;

namespace PMPlatform.Api.Models.AuditActivity;

/// <summary>Shape validation of ADM-050's and SCR-057's query strings (T-3: shape here, meaning in the module).</summary>
internal static partial class AuditActivityQueries
{
    private const int EventTypeLength = 100;
    private const int SubjectTypeLength = 100;

    public static AuditEventQuery Events(
        string? category, DateTimeOffset? occurredFrom, DateTimeOffset? occurredTo, Guid? actorUserId, string? eventClass, string? eventType,
        string? outcome, Guid? projectId, string? subjectType, Guid? subjectId, Guid? correlationId, List<FieldError> errors)
    {
        RequestValidation.Optional(eventType, "eventType", EventTypeLength, errors);
        RequestValidation.Optional(subjectType, "subjectType", SubjectTypeLength, errors);
        Range(occurredFrom, occurredTo, errors);
        return new AuditEventQuery
        {
            Categories = QueryParameters.EnumSet<AuditCategory>(category, "category", errors),
            OccurredFrom = occurredFrom,
            OccurredTo = occurredTo,
            ActorUserId = actorUserId,
            EventClasses = QueryParameters.EnumSet<AuditEventClass>(eventClass, "eventClass", errors),
            EventType = eventType,
            Outcomes = QueryParameters.EnumSet<AuditOutcome>(outcome, "outcome", errors),
            ProjectId = projectId,
            SubjectType = subjectType,
            SubjectId = subjectId,
            CorrelationId = correlationId,
        };
    }

    public static ActivityQuery Activity(
        Guid? projectId, string? subjectType, Guid? subjectId, string? activityKind, Guid? actorUserId, DateTimeOffset? occurredFrom, DateTimeOffset? occurredTo,
        List<FieldError> errors)
    {
        RequestValidation.Optional(subjectType, "subjectType", SubjectTypeLength, errors);
        Range(occurredFrom, occurredTo, errors);
        List<string> kinds = string.IsNullOrEmpty(activityKind) ? [] : [.. activityKind.Split(',')];
        if (kinds.Any(k => !KindPattern().IsMatch(k)))
        {
            errors.Add(new FieldError("activityKind", FieldError.Malformed));
            kinds = [];
        }

        return new ActivityQuery
        {
            ProjectId = projectId,
            SubjectType = subjectType,
            SubjectId = subjectId,
            ActivityKinds = kinds,
            ActorUserId = actorUserId,
            OccurredFrom = occurredFrom,
            OccurredTo = occurredTo,
        };
    }

    private static void Range(DateTimeOffset? from, DateTimeOffset? to, List<FieldError> errors)
    {
        if (from is { } start && to is { } end && end < start)
        {
            errors.Add(new FieldError("occurredTo", FieldError.DateBeforeStart));
        }
    }

    /// <summary>A source domain as the Activity line names it: upper snake case, at most 50 characters.</summary>
    [GeneratedRegex("^[A-Z][A-Z0-9_]{0,49}$", RegexOptions.CultureInvariant)]
    private static partial Regex KindPattern();
}
