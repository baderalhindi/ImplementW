using PMPlatform.Application.Features.Dashboards.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Dashboards;

namespace PMPlatform.Application.Features.Dashboards;

/// <summary>
/// FG-01 §13.4's publication validation of a version's content (DSH-CC-06 to -08, -27, -28): every widget binds a registered projection
/// in a context and a widget type it supports; widgets do not overlap; a widget is optional only where personalisation is allowed, and
/// personalisation only where ADR-019 allows it; the audience is named, once each, and only with roles the dashboard may be offered to.
/// Bilingual labels are complete by construction (ADR-012, <c>BilingualLabel</c>). Each refusal is one <c>errors[]</c> item.
/// </summary>
internal static class DashboardDefinitionRules
{
    public static IReadOnlyList<FieldIssue> Check(DashboardCode code, DashboardDefinitionContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        List<FieldIssue> issues = [];
        DashboardContextKind context = DashboardCatalogue.ContextOf(code);

        if (content.AllowsPersonalization && !DashboardCatalogue.MayPersonalize(code))
        {
            issues.Add(new FieldIssue("allowsPersonalization", FieldIssue.NotAllowed));
        }

        if (content.Audience.Count == 0)
        {
            issues.Add(new FieldIssue("audience", FieldIssue.Required));
        }

        HashSet<string> roles = new(StringComparer.Ordinal);
        for (int i = 0; i < content.Audience.Count; i++)
        {
            DashboardAudienceInput member = content.Audience[i];
            if (!roles.Add(member.RoleCode))
            {
                issues.Add(new FieldIssue($"audience[{i}].roleCode", FieldIssue.Duplicate));
            }
            else if (!DashboardCatalogue.MayBeOfferedTo(code, member.RoleCode))
            {
                issues.Add(new FieldIssue($"audience[{i}].roleCode", DashboardIssueCodes.AudienceNotPermitted));
            }
        }

        if (content.Widgets.Count == 0)
        {
            issues.Add(new FieldIssue("widgets", FieldIssue.Required));
        }

        HashSet<string> codes = new(StringComparer.Ordinal);
        for (int i = 0; i < content.Widgets.Count; i++)
        {
            DashboardWidgetInput widget = content.Widgets[i];
            string at = $"widgets[{i}]";
            if (!codes.Add(widget.Code))
            {
                issues.Add(new FieldIssue($"{at}.code", FieldIssue.Duplicate));
            }

            if (DashboardProjections.Find(widget.SourceProjectionCode) is not { } projection)
            {
                issues.Add(new FieldIssue($"{at}.sourceProjectionCode", DashboardIssueCodes.ProjectionNotRegistered));
            }
            else
            {
                if (!projection.Supports(context))
                {
                    issues.Add(new FieldIssue($"{at}.sourceProjectionCode", DashboardIssueCodes.ContextNotSupported));
                }

                if (!projection.WidgetTypes.Contains(widget.WidgetType))
                {
                    issues.Add(new FieldIssue($"{at}.widgetType", DashboardIssueCodes.WidgetTypeNotSupported));
                }
            }

            if (widget.IsOptionalVisibility && !content.AllowsPersonalization)
            {
                issues.Add(new FieldIssue($"{at}.isOptionalVisibility", FieldIssue.NotAllowed));
            }

            if (content.Widgets.Take(i).Any(other => Overlaps(other, widget)))
            {
                issues.Add(new FieldIssue($"{at}.layoutColumn", DashboardIssueCodes.LayoutOverlap));
            }
        }

        return issues;
    }

    /// <summary>At publication: a role already landing on another PUBLISHED dashboard cannot land on this one too (Blueprint §20.2).</summary>
    public static IReadOnlyList<FieldIssue> CheckLandings(DashboardDefinitionContent content, IReadOnlySet<string> rolesLandingElsewhere)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(rolesLandingElsewhere);
        return
        [
            .. content.Audience.Select((member, i) => (member, i))
                .Where(x => x.member.IsDefaultLanding && rolesLandingElsewhere.Contains(x.member.RoleCode))
                .Select(x => new FieldIssue($"audience[{x.i}].isDefaultLanding", DashboardIssueCodes.DefaultLandingTaken)),
        ];
    }

    private static bool Overlaps(DashboardWidgetInput a, DashboardWidgetInput b) =>
        a.LayoutRow == b.LayoutRow && a.LayoutColumn < b.LayoutColumn + b.LayoutSpan && b.LayoutColumn < a.LayoutColumn + a.LayoutSpan;
}
