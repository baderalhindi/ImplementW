using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.AuditActivity.Contracts;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// Operating the FG-06 audit store (TASK-073; AUD-API-035, -037, -039, -040): the pipeline's health, the integrity of the hash
/// chain, and the Activity projection checked against the store and rebuilt from it. Reveals counts and event ids, never an event's
/// content. Each verification, check and rebuild is itself audited.
/// </summary>
[Tags("AuditActivity")]
public sealed class AuditOperationsController(IAuditOperationsService operations) : AdministrationControllerBase
{
    /// <summary>What is captured and not yet stored, what keeps failing, and how far the Activity projection trails the store.</summary>
    [HttpGet("api/v1/audit-ingestion-status")]
    [RequirePermission(PermissionCatalogue.AuditOperationsManage)]
    [ProducesResponseType<AuditIngestionStatus>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("AuditActivity_GetAuditIngestionStatus")]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken) => Ok(await operations.GetStatusAsync(cancellationToken));

    /// <summary>Walks the whole hash chain and every attribute digest; reports what no longer matches (AUD-CC-27).</summary>
    [HttpPost("api/v1/audit-integrity-verifications")]
    [RequirePermission(PermissionCatalogue.AuditOperationsManage)]
    [SensitiveWrite]
    [ProducesResponseType<AuditIntegrityReport>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("AuditActivity_VerifyAuditIntegrity")]
    public async Task<IActionResult> Verify(CancellationToken cancellationToken) => Ok(await operations.VerifyIntegrityAsync(CallerId, cancellationToken));

    /// <summary>Compares the Activity projection with what the store derives, up to the projection's checkpoint; changes nothing.</summary>
    [HttpPost("api/v1/activity-projection-checks")]
    [RequirePermission(PermissionCatalogue.AuditOperationsManage)]
    [SensitiveWrite]
    [ProducesResponseType<ActivityProjectionReport>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("AuditActivity_CheckActivityProjection")]
    public async Task<IActionResult> Check(CancellationToken cancellationToken) => Ok(await operations.CheckProjectionAsync(CallerId, cancellationToken));

    /// <summary>Derives the Activity projection again from every stored event and repairs each difference (FG-06 §4.5).</summary>
    [HttpPost("api/v1/activity-projection-rebuilds")]
    [RequirePermission(PermissionCatalogue.AuditOperationsManage)]
    [SensitiveWrite]
    [ProducesResponseType<ActivityProjectionReport>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("AuditActivity_RebuildActivityProjection")]
    public async Task<IActionResult> Rebuild(CancellationToken cancellationToken) => Ok(await operations.RebuildProjectionAsync(CallerId, cancellationToken));
}
