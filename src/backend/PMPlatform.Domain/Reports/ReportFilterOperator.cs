namespace PMPlatform.Domain.Reports;

/// <summary>The closed set of filter operators (ERD <c>saved_view_filter.operator</c>): no expression, formula or query text (BR-RPT-046).</summary>
public enum ReportFilterOperator
{
    Eq = 1,
    Neq = 2,
    In = 3,
    Gt = 4,
    Gte = 5,
    Lt = 6,
    Lte = 7,
    Between = 8,
    Contains = 9,
}
