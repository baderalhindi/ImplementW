using System.Security.Cryptography;
using System.Text;
using PMPlatform.Application.Features.DocumentManagement;
using PMPlatform.Application.Features.DocumentManagement.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.DocumentManagement;

namespace PMPlatform.Tests.Unit.Application.DocumentManagement;

/// <summary>SR-38 (DOC-T01): an upload is accepted only when its extension and leading bytes agree with its declared type.</summary>
public sealed class DocumentContentRulesTests
{
    private const string Pdf = "application/pdf";
    private const string Docx = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private const string Xls = "application/vnd.ms-excel";
    private const string Png = "image/png";
    private const string Jpeg = "image/jpeg";
    private const string Text = "text/plain";
    private const string Csv = "text/csv";

    private static readonly byte[] PdfHeader = "%PDF-1.7\n"u8.ToArray();
    private static readonly byte[] ZipHeader = [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00];
    private static readonly byte[] OleHeader = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0x00];
    private static readonly byte[] PngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];
    private static readonly byte[] JpegHeader = [0xFF, 0xD8, 0xFF, 0xE0];

    public static TheoryData<string, byte[], bool> Contents => new()
    {
        { Pdf, PdfHeader, true },
        { Pdf, ZipHeader, false },
        { Pdf, "%PD"u8.ToArray(), false },
        { Docx, ZipHeader, true },
        { Docx, OleHeader, false },
        { Xls, OleHeader, true },
        { Xls, ZipHeader, false },
        { Png, PngHeader, true },
        { Png, JpegHeader, false },
        { Jpeg, JpegHeader, true },
        { Jpeg, "MZ\x90"u8.ToArray(), false },
        { Text, "Minutes of the steering committee"u8.ToArray(), true },
        { Csv, Encoding.UTF8.GetBytes("الاسم,القيمة\nمشروع,1\n"), true },
        { Text, "MZ\x90\x00 an executable"u8.ToArray(), false },
        { Text, "MZ is not text here"u8.ToArray(), false },
        { Text, [0x7F, 0x45, 0x4C, 0x46, 0x02], false },
        { Csv, ZipHeader, false },
        { Csv, PdfHeader, false },
        { Text, "text\0with a NUL"u8.ToArray(), false },
        { Text, [], false },
        { "application/x-msdownload", "MZ"u8.ToArray(), false },
    };

    [Theory]
    [MemberData(nameof(Contents))]
    public void TheLeadingBytesMustBeTheDeclaredTypes(string mediaType, byte[] header, bool accepted) =>
        Assert.Equal(accepted, DocumentContentRules.ContentMatches(mediaType, header));

    [Fact]
    public void ANulAnywhereInTheFirstBlockMakesAFileNotText()
    {
        byte[] header = Encoding.ASCII.GetBytes(new string('a', DocumentContentRules.HeaderLength));
        header[^1] = 0;

        Assert.False(DocumentContentRules.ContentMatches(Text, header));
    }

    [Theory]
    [InlineData(Pdf, "report.pdf", true)]
    [InlineData(Pdf, "REPORT.PDF", true)]
    [InlineData(Pdf, "report.pdf.exe", false)]
    [InlineData(Pdf, "report", false)]
    [InlineData(Pdf, "report.docx", false)]
    [InlineData(Docx, "minutes.docx", true)]
    [InlineData(Docx, "minutes.docm", false)]
    [InlineData(Xls, "budget.xls", true)]
    [InlineData(Xls, "budget.xlsx", false)]
    [InlineData(Jpeg, "site.jpg", true)]
    [InlineData(Jpeg, "site.JPEG", true)]
    [InlineData(Png, "site.jpg", false)]
    [InlineData(Text, "notes.txt", true)]
    [InlineData(Text, "notes.csv", false)]
    [InlineData(Csv, "list.csv", true)]
    [InlineData(Text, "page.html", false)]
    [InlineData("application/x-msdownload", "setup.exe", false)]
    public void TheFileNameMustEndInAnExtensionOfTheDeclaredType(string mediaType, string fileName, bool accepted) =>
        Assert.Equal(accepted, DocumentContentRules.ExtensionMatches(mediaType, fileName));

    [Fact]
    public void EveryShippedMediaTypeHasARuleAndAnUnknownOneDoesNot()
    {
        DocumentUploadPolicy policy = new() { MaxFileSizeBytes = 10 };
        foreach (string type in new[] { Pdf, "application/msword", Docx, Xls, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                     "application/vnd.ms-powerpoint", "application/vnd.openxmlformats-officedocument.presentationml.presentation", Png, Jpeg, Text, Csv })
        {
            policy.AllowedContentTypes.Add(type);
        }

        Assert.True(policy.EveryTypeIsVerifiable);
        policy.AllowedContentTypes.Add("application/zip");
        Assert.False(policy.EveryTypeIsVerifiable);
    }

    [Theory]
    [InlineData("report.pdf", "application/pdf; name=report", true)]
    [InlineData("report.pdf", "APPLICATION/PDF", true)]
    [InlineData("report.exe", "application/pdf", false)]
    [InlineData("report.pdf", "text/plain", false)]
    public async Task TheCheckJudgesTheDeclaredTypeTheNameAndTheBytesTogether(string fileName, string contentType, bool accepted)
    {
        await using MemoryStream content = new([.. PdfHeader, .. "body"u8.ToArray()]);

        AdministrationResult<DocumentFile> result = await Uploads().CheckAsync(new DocumentFile(fileName, contentType, content.Length, content), CancellationToken.None);

        Assert.Equal(accepted, result.Succeeded);
        if (!accepted)
        {
            Assert.Equal(AdministrationErrorKind.UnsupportedMediaType, result.Error!.Kind);
        }
    }

    [Fact]
    public async Task ASpoofedFileIsRefusedAndNothingIsStored()
    {
        InMemoryStorage storage = new();
        await using MemoryStream content = new([0x4D, 0x5A, 0x90, 0x00, 0x03]);

        AdministrationResult<DocumentFile> result = await Uploads(storage).CheckAsync(new DocumentFile("invoice.pdf", Pdf, content.Length, content), CancellationToken.None);

        Assert.Equal(AdministrationErrorKind.UnsupportedMediaType, result.Error?.Kind);
        Assert.Empty(storage.Objects);
    }

    [Fact]
    public async Task AnAcceptedFileIsStoredWholeFromAStreamThatCannotSeekWithItsChecksumAndSize()
    {
        byte[] bytes = [.. PdfHeader, .. RandomNumberGenerator.GetBytes(300_000)];
        InMemoryStorage storage = new();
        DocumentUploads uploads = Uploads(storage);
        await using ForwardOnlyStream content = new(bytes);

        AdministrationResult<DocumentFile> accepted = await uploads.CheckAsync(new DocumentFile("report.pdf", Pdf, bytes.LongLength, content), CancellationToken.None);
        Assert.True(accepted.Succeeded);
        DocumentVersion version = await uploads.StoreAsync(Guid.NewGuid(), 1, accepted.Value, Guid.NewGuid(), DateTimeOffset.UnixEpoch, CancellationToken.None);

        Assert.Equal(bytes, storage.Objects[version.StorageObjectKey]);
        Assert.Equal((bytes.LongLength, Convert.ToHexStringLower(SHA256.HashData(bytes))), (version.SizeBytes, version.ChecksumSha256));
    }

    private static DocumentUploads Uploads(InMemoryStorage? storage = null)
    {
        DocumentUploadPolicy policy = new() { MaxFileSizeBytes = 1_000_000 };
        policy.AllowedContentTypes.Add(Pdf);
        policy.AllowedContentTypes.Add(Text);
        return new DocumentUploads(storage ?? new InMemoryStorage(), policy);
    }

    private sealed class InMemoryStorage : IDocumentStorage
    {
        public Dictionary<string, byte[]> Objects { get; } = [];

        public bool IsConfigured => true;

        public async Task WriteAsync(string objectKey, Stream content, string contentType, CancellationToken cancellationToken)
        {
            await using MemoryStream copy = new();
            await content.CopyToAsync(copy, cancellationToken);
            Objects.Add(objectKey, copy.ToArray());
        }

        public Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken) =>
            Task.FromResult<Stream>(new MemoryStream(Objects[objectKey]));
    }

    /// <summary>A multipart body as it streams: no seeking, and at most 1000 bytes a read.</summary>
    private sealed class ForwardOnlyStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;

        public override long Seek(long offset, SeekOrigin loc) => throw new NotSupportedException();

        public override int Read(Span<byte> buffer) => base.Read(buffer[..Math.Min(buffer.Length, 1000)]);

        public override int Read(byte[] buffer, int offset, int count) => base.Read(buffer, offset, Math.Min(count, 1000));

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(buffer.Length, 1000)], cancellationToken);
    }
}
