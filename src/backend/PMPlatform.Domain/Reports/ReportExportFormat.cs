namespace PMPlatform.Domain.Reports;

/// <summary>The export baseline (ADR-005, ICD-17): PDF, XLSX and CSV only. A format never multiplies the report count (ADR-006).</summary>
public enum ReportExportFormat
{
    Pdf = 1,
    Xlsx = 2,
    Csv = 3,
}
