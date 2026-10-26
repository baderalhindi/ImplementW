namespace PMPlatform.Domain.ProjectTask;

/// <summary>
/// ERD <c>task_dependency.dependency_type</c>: which point of the predecessor gates which step of the successor. The first letter
/// is the predecessor's point (Start = started, Finish = completed), the second the successor's step it gates.
/// </summary>
public enum TaskDependencyType
{
    /// <summary>The successor starts once the predecessor is completed.</summary>
    Fs = 1,

    /// <summary>The successor starts once the predecessor has started.</summary>
    Ss = 2,

    /// <summary>The successor completes once the predecessor is completed.</summary>
    Ff = 3,

    /// <summary>The successor completes once the predecessor has started.</summary>
    Sf = 4,
}
