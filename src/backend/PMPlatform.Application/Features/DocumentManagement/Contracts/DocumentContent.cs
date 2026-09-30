namespace PMPlatform.Application.Features.DocumentManagement.Contracts;

/// <summary>The content of a CLEAN version, to be served as an attachment. The receiver disposes <see cref="Content"/>.</summary>
public sealed record DocumentContent(string FileName, long Length, string ChecksumSha256, Stream Content);
