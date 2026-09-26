namespace PMPlatform.Application.Common.Authorization;

/// <summary>Whether a permission reads or changes data. A READ-ONLY grant never satisfies a <see cref="Write"/> permission.</summary>
public enum AccessMode
{
    Read = 1,
    Write = 2,
}
