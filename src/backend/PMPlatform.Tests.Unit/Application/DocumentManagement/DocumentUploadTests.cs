using System.Security.Cryptography;
using PMPlatform.Application.Features.DocumentManagement;
using PMPlatform.Application.Features.DocumentManagement.Contracts;

namespace PMPlatform.Tests.Unit.Application.DocumentManagement;

/// <summary>Secure upload's pure parts (TASK-037): what is accepted, the name kept, and the checksum taken in the same pass.</summary>
public sealed class DocumentUploadTests
{
    [Theory]
    [InlineData("application/pdf", true)]
    [InlineData("APPLICATION/PDF", true)]
    [InlineData("text/plain; charset=utf-8", true)]
    [InlineData("application/x-msdownload", false)]
    [InlineData("", false)]
    public void TheUploadPolicyAcceptsOnlyItsMediaTypesIgnoringCaseAndParameters(string contentType, bool accepted)
    {
        DocumentUploadPolicy policy = new() { MaxFileSizeBytes = 10 };
        policy.AllowedContentTypes.Add("application/pdf");
        policy.AllowedContentTypes.Add("text/plain");

        Assert.Equal(accepted, policy.Allows(contentType));
    }

    [Theory]
    [InlineData("report.pdf", "report.pdf")]
    [InlineData(@"C:\Users\someone\secret\report.pdf", "report.pdf")]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("tab\there.pdf", "tabhere.pdf")]
    [InlineData("..", DocumentFileNames.Fallback)]
    [InlineData("   ", DocumentFileNames.Fallback)]
    [InlineData(null, DocumentFileNames.Fallback)]
    public void AFileNameKeepsItsLastSegmentWithoutControlCharacters(string? given, string kept) =>
        Assert.Equal(kept, DocumentFileNames.Clean(given));

    [Fact]
    public void AFileNameIsCutToTheColumnsLength() =>
        Assert.Equal(DocumentFileNames.MaxLength, DocumentFileNames.Clean(new string('a', 600) + ".pdf").Length);

    [Fact]
    public async Task TheChecksumAndSizeAreTakenFromTheBytesAsTheyAreStored()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(300_000);
        await using MemoryStream source = new(bytes);
        await using MemoryStream stored = new();

        await using (HashingReadStream hashing = new(source))
        {
            await hashing.CopyToAsync(stored);
            Assert.Equal((bytes.LongLength, Convert.ToHexStringLower(SHA256.HashData(bytes))), (hashing.BytesRead, hashing.Sha256Hex()));
        }

        Assert.Equal(bytes, stored.ToArray());
    }
}
