using System.Net;
using Microsoft.Extensions.Options;
using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.MasterDataConfig;

namespace PMPlatform.Tests.Integration.DocumentManagement;

/// <summary>
/// The four scan states (TASK-037, CTL-20): SCAN_FAILED is final until queued again; a scanner that cannot be reached leaves
/// the version pending; with no scanner nothing is ever CLEAN (fail closed); with no store nothing is uploaded; a store
/// named in neither accepted form stops the API.
/// </summary>
[Collection(DocumentSuite.Name)]
public sealed class ScanLifecycleTests(DocumentTestHost host)
{
    [Fact]
    public async Task AnUndecidableFileIsScanFailedUntilItIsQueuedAgainAndDecided()
    {
        using HttpClient client = host.Api.CreateClient();
        string controller = (await client.SignInOrFailAsync(2)).AccessToken;
        string manager = (await client.SignInOrFailAsync(8)).AccessToken;
        (Guid documentId, Guid versionId) = await client.UploadOrFailAsync(
            controller, DocumentDriver.Text($"{TestMalwareScanner.UnscannableMarker} {Guid.NewGuid()}"), DocumentTestHost.EntityProjectId);
        string rescan = $"{DocumentDriver.Documents}/{documentId}/versions/{versionId}/rescan";
        string content = $"{DocumentDriver.Documents}/{documentId}/versions/{versionId}/content";

        await host.Api.ScanAsync();
        Assert.Equal("SCAN_FAILED", await host.ScanStateAsync(versionId));
        await AssertAnswerAsync(HttpStatusCode.Conflict, "DOCUMENT_NOT_AVAILABLE", client.GetAsync(content, controller));

        // Queuing again is managing the document: the entity Project Manager may not.
        await AssertAnswerAsync(HttpStatusCode.Forbidden, "PERMISSION_DENIED", client.PostAsync(rescan, manager));
        host.Scanner.RefusesMarker = false;
        try
        {
            using (HttpResponseMessage queued = await client.PostAsync(rescan, controller))
            {
                Assert.Equal("SCAN_PENDING", (await queued.ReadObjectAsync())["scanState"]!.GetValue<string>());
            }

            await AssertAnswerAsync(HttpStatusCode.Conflict, "INVALID_TRANSITION", client.PostAsync(rescan, controller));
            await host.Api.ScanAsync();
        }
        finally
        {
            host.Scanner.RefusesMarker = true;
        }

        Assert.Equal("CLEAN", await host.ScanStateAsync(versionId));
        using HttpResponseMessage served = await client.GetAsync(content, controller);
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
    }

    [Fact]
    public async Task AScannerThatCannotBeReachedLeavesTheVersionPendingForTheNextPass()
    {
        using HttpClient client = host.Api.CreateClient();
        string token = (await client.SignInOrFailAsync(2)).AccessToken;
        (_, Guid versionId) = await client.UploadOrFailAsync(token, DocumentDriver.Text($"outage {Guid.NewGuid()}"));
        string triedBefore = (await host.Database.QueryAsync($"SELECT updated_at::text FROM document_management.document_version WHERE id = '{versionId}'")).Single();

        host.Scanner.Unreachable = true;
        try
        {
            await host.Api.ScanAsync();
        }
        finally
        {
            host.Scanner.Unreachable = false;
        }

        // Still pending, sent to the back of the queue, with no verdict recorded.
        Assert.Equal("SCAN_PENDING", await host.ScanStateAsync(versionId));
        Assert.NotEqual(triedBefore, (await host.Database.QueryAsync($"SELECT updated_at::text FROM document_management.document_version WHERE id = '{versionId}'")).Single());

        await host.Api.ScanAsync();
        Assert.Equal("CLEAN", await host.ScanStateAsync(versionId));
    }

    /// <summary>No provider is selected (F-2): the platform's own scanner scans nothing, so nothing becomes CLEAN or served.</summary>
    [Fact]
    public async Task WithoutAScannerNothingIsEverClean()
    {
        await using IdentityApiFactory unscanned = host.CreateApi(withScanner: false);
        using HttpClient client = unscanned.CreateClient();
        string token = (await client.SignInOrFailAsync(2)).AccessToken;
        (Guid documentId, Guid versionId) = await client.UploadOrFailAsync(token, DocumentDriver.Text($"unscanned {Guid.NewGuid()}"));

        Assert.Equal(0, await unscanned.ScanAsync());
        Assert.Equal("SCAN_PENDING", await host.ScanStateAsync(versionId));
        await AssertAnswerAsync(HttpStatusCode.Conflict, "DOCUMENT_NOT_AVAILABLE", client.GetAsync($"{DocumentDriver.Documents}/{documentId}/versions/{versionId}/content", token));
    }

    [Fact]
    public async Task WithoutAStoreNothingIsUploadedAndTheApiStillStarts()
    {
        await using IdentityApiFactory storeless = host.CreateApi(new Dictionary<string, string?> { ["DOCUMENT_STORAGE_CONNECTION_STRING"] = "" });
        using HttpClient client = storeless.CreateClient();
        string token = (await client.SignInOrFailAsync(2)).AccessToken;

        await AssertAnswerAsync(HttpStatusCode.ServiceUnavailable, "UNAVAILABLE", client.UploadAsync(token, DocumentDriver.Text("x")));
    }

    [Theory]
    [InlineData("s3://pmplatform-documents")]
    [InlineData("gs://Not_A_Bucket")]
    [InlineData("file://relative/documents")]
    public async Task AStoreInNeitherAcceptedFormStopsTheApi(string connectionString)
    {
        await using IdentityApiFactory misconfigured = host.CreateApi(new Dictionary<string, string?> { ["DOCUMENT_STORAGE_CONNECTION_STRING"] = connectionString });

        OptionsValidationException refused = Assert.Throws<OptionsValidationException>(misconfigured.CreateClient);
        Assert.DoesNotContain(connectionString, refused.Message, StringComparison.Ordinal);
    }

    private static async Task AssertAnswerAsync(HttpStatusCode status, string code, Task<HttpResponseMessage> call)
    {
        using HttpResponseMessage response = await call;
        Assert.Equal((status, code), (response.StatusCode, await response.CodeOfAsync()));
    }
}
