using PMPlatform.Domain.Common;
using PMPlatform.Domain.Reports;

namespace PMPlatform.Application.Features.Reports.Contracts;

/// <summary>A parameter's value, as text: a project or department id, or an option's value.</summary>
public sealed record ReportParameterInput(string Code, string Value);

/// <summary>A field by the names configuration gives it: its source entity (<c>PROJECT</c>, or a projection's code without its dot) and its code.</summary>
public sealed record ReportFieldReference(string SourceEntityCode, string FieldCode);

public sealed record ReportSortInput(string SourceEntityCode, string FieldCode, ReportSortDirection Direction);

/// <summary>A filter: a field, an operator of the closed set and a value — for IN several, for BETWEEN two, separated by commas. Never an expression.</summary>
public sealed record ReportFilterInput(string SourceEntityCode, string FieldCode, ReportFilterOperator Operator, string Value);

/// <summary>
/// A run of a published report: its parameters, the columns chosen among those it publishes (its default columns when null), and the order.
/// </summary>
public sealed record ReportRunInput(IReadOnlyList<ReportParameterInput> Parameters, IReadOnlyList<ReportFieldReference>? Columns, IReadOnlyList<ReportSortInput> Sort);

/// <summary>An SCR-138 composition: allowlisted columns, filters and sorts only (ADR-019; BR-RPT-045).</summary>
public sealed record ExplorerRunInput(IReadOnlyList<ReportFieldReference> Columns, IReadOnlyList<ReportFilterInput> Filters, IReadOnlyList<ReportSortInput> Sort);

/// <summary>MOD-060: the format (ADR-005) and the language of the output.</summary>
public sealed record ReportExportInput(ReportExportFormat Format, Language Language);
