namespace PMPlatform.Application.Features.DocumentManagement.Contracts;

/// <summary>An uploaded file: the name and media type the uploader gave, and its bytes. The caller owns the stream.</summary>
public sealed record DocumentFile(string FileName, string ContentType, long Length, Stream Content);
