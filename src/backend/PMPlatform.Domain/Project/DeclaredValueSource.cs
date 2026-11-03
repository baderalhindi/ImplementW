namespace PMPlatform.Domain.Project;

/// <summary>Where a project's declared progress and spend came from (R3-3): an import batch, or a person on the follow-up screen.</summary>
public enum DeclaredValueSource
{
    Import = 1,
    Manual = 2,
}
