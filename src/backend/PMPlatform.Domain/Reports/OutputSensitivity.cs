namespace PMPlatform.Domain.Reports;

/// <summary>
/// An output's classification as its content makes it (FG-02 §9.3, GEN-009): SENSITIVE when it reveals a financial amount or a classified
/// column, whatever the report's baseline; STANDARD otherwise. ADR-010's taxonomy, when AHDA supplies it, refines it.
/// </summary>
public enum OutputSensitivity
{
    Standard = 1,
    Sensitive = 2,
}
