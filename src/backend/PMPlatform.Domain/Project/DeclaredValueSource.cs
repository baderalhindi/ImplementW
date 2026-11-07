namespace PMPlatform.Domain.Project;

/// <summary>Where a project's declared progress and spend came from (R3-3): an import batch, a person on the follow-up screen, or an accepted monthly update from the entity (R4-2).</summary>
public enum DeclaredValueSource
{
    Import = 1,
    Manual = 2,
    EntityUpdate = 3,
    ProgressUpdate = 4,
}
