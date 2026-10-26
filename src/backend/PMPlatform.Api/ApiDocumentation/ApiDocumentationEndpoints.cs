using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerUI;

namespace PMPlatform.Api.ApiDocumentation;

/// <summary>
/// The OpenAPI document and its docs UI (TASK-094, api-conventions R-49). The document is generated from the code, never written by
/// hand: served at <see cref="DocumentPath"/>, written to a file by the <see cref="Command"/> command for CI, and browsable in Swagger UI
/// at <see cref="UiPath"/> where <see cref="ApiDocumentationOptions.Enabled"/>.
/// </summary>
internal static class ApiDocumentationEndpoints
{
    /// <summary><c>dotnet PMPlatform.Api.dll openapi &lt;file&gt;</c> writes the document and exits without serving.</summary>
    public const string Command = "openapi";

    public const string DocumentName = "v1";

    public const string DocumentPath = "/openapi/v1.json";

    public const string UiPath = "/api-docs";

    /// <summary>
    /// The docs UI's policy, in place of the API's <c>default-src 'none'</c> on <see cref="UiPath"/> alone: its own scripts, styles and
    /// images, and the document from the same origin. Nothing inline, nothing from another origin, and still never framed.
    /// </summary>
    public const string UiContentSecurityPolicy =
        "default-src 'none'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; " +
        "frame-ancestors 'none'; base-uri 'none'; form-action 'none'";

    /// <summary>How the document is written to a file: the form the contract snapshot <c>docs/api/openapi.v1.json</c> is committed in.</summary>
    private static readonly JsonSerializerOptions Written = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static bool IsEnabled(this IServiceProvider services) =>
        services.GetRequiredService<IOptions<ApiDocumentationOptions>>().Value.Enabled;

    /// <summary>
    /// The document wherever the API runs as Development (the contract tests read it) or documentation is enabled, and the docs UI where
    /// documentation is enabled. Both anonymous: the document is the committed snapshot of a public repository, so it discloses nothing
    /// the source does not. The UI only reads; it sends no request to the API but the document's (no "Try it out").
    /// </summary>
    public static void UseApiDocumentation(this WebApplication app)
    {
        bool enabled = app.Services.IsEnabled();
        if (app.Environment.IsDevelopment() || enabled)
        {
            app.MapOpenApi().AllowAnonymous();
        }

        if (enabled)
        {
            app.UseSwaggerUI(ui =>
            {
                ui.RoutePrefix = UiPath.TrimStart('/');
                ui.DocumentTitle = "PMPlatform API v1";
                ui.SwaggerEndpoint(DocumentPath, "PMPlatform API v1");
                ui.SupportedSubmitMethods([]);
                ui.DisplayOperationId();

                // One section per module (R-51), closed until opened: with every section open, Swagger UI stops rendering the
                // 400-operation document after about ten operations; opened one at a time, each renders in full (docs/api/README.md D-5).
                ui.DocExpansion(DocExpansion.None);
            });
        }
    }

    /// <summary>
    /// Writes the document to <paramref name="path"/> without <c>servers</c> (the host is not part of the contract), in the snapshot's
    /// form, so a byte comparison with <c>docs/api/openapi.v1.json</c> is a comparison of contracts.
    /// </summary>
    public static async Task<int> WriteDocumentAsync(this IServiceProvider services, string path)
    {
        OpenApiDocument document =
            await services.GetRequiredKeyedService<IOpenApiDocumentProvider>(DocumentName).GetOpenApiDocumentAsync().ConfigureAwait(false);
        using StringWriter json = new();
        document.SerializeAsV31(new OpenApiJsonWriter(json));

        JsonObject root = JsonNode.Parse(json.ToString())!.AsObject();
        root.Remove("servers");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        await File.WriteAllTextAsync(path, root.ToJsonString(Written) + "\n").ConfigureAwait(false);
        return 0;
    }
}
