namespace PMPlatform.Api.ApiDocumentation;

/// <summary>
/// The hosted API documentation (TASK-094): <c>ApiDocumentation:Enabled</c>, off unless an environment turns it on. DEV and SIT turn it
/// on (infra/terraform, <c>api_documentation</c>); UAT and PROD cannot (platform/guards.tf).
/// </summary>
internal sealed class ApiDocumentationOptions
{
    public const string Section = "ApiDocumentation";

    /// <summary>Serves the generated document at <see cref="ApiDocumentationEndpoints.DocumentPath"/> and the docs UI at <see cref="ApiDocumentationEndpoints.UiPath"/>.</summary>
    public bool Enabled { get; set; }
}
