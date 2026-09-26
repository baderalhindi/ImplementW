using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Tests.Integration.Identity;

/// <summary>
/// ADR-010 masking read from the database: the FIELD_CLASSIFICATION version in force (PUBLISHED and effective) and the
/// DATA_CLASSIFICATION order, against the clearance of the permission a field is read under. The taxonomy is
/// outstanding (UGV-01), so the levels and the field here are the test's own.
/// </summary>
[Collection(IdentitySuite.Name)]
public sealed class FieldClassificationTests(IdentityTestHost host)
{
    private const string Entity = "TEST_ENTITY";
    private const string LowLevel = "00000000-0131-4000-8000-00000000c001";
    private const string HighLevel = "00000000-0131-4000-8000-00000000c002";
    private const string PublishedVersion = "00000000-0140-4000-8000-00000000c001";
    private const string DraftVersion = "00000000-0140-4000-8000-00000000c002";

    [Fact]
    public async Task AFieldAboveThePermissionsClearanceIsMaskedAndADraftClassificationIsNot()
    {
        try
        {
            await host.Database.ExecuteAsync(Classifications);

            await SetClearanceAsync(LowLevel);
            FieldMask low = await MaskAsync();
            await SetClearanceAsync(HighLevel);
            FieldMask high = await MaskAsync();

            Assert.Equal(["budget"], low.MaskedFields);
            Assert.Equal(MaskingRule.Withhold, low.RuleFor("budget"));
            // "title" is classified only in the DRAFT version: later and already "effective", but not PUBLISHED, so not in force.
            Assert.True(low.Reveals("title"));
            Assert.Empty(high.MaskedFields);
        }
        finally
        {
            await SetClearanceAsync(null);
            await host.Database.ExecuteAsync($"""
                DELETE FROM master_data_config.configuration_version WHERE id IN ('{PublishedVersion}', '{DraftVersion}');
                DELETE FROM master_data_config.master_data_item WHERE id IN ('{LowLevel}', '{HighLevel}');
                """);
        }
    }

    /// <summary>A fresh scope per call: the engine reads once per request.</summary>
    private async Task<FieldMask> MaskAsync()
    {
        using IServiceScope scope = host.Api.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IAuthorizationEngine>().GetFieldMaskAsync(
            Guid.Parse(IdentityDatabase.UserId(1)), PermissionCatalogue.IdentityIntegrationManage, Entity, CancellationToken.None);
    }

    private Task SetClearanceAsync(string? itemId) =>
        host.Database.ExecuteAsync(
            $"UPDATE identity_access.permission SET data_classification_item_id = {(itemId is null ? "NULL" : $"'{itemId}'")} WHERE code = '{PermissionCatalogue.IdentityIntegrationManage}'");

    private static string Classifications => $"""
        INSERT INTO master_data_config.master_data_item (id, catalogue_id, code, label_ar, label_en, sort_order, lifecycle_state, created_at, created_by, updated_at, updated_by)
        SELECT v.id::uuid, c.id, v.code, 'مستوى', v.code, v.sort_order, 'PUBLISHED', now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}'
        FROM master_data_config.master_data_catalogue c,
             (VALUES ('{LowLevel}', 'TEST_LOW', 1), ('{HighLevel}', 'TEST_HIGH', 2)) AS v (id, code, sort_order)
        WHERE c.code = 'DATA_CLASSIFICATION';

        INSERT INTO master_data_config.configuration_version (id, configuration_family_id, version_no, lifecycle_state, effective_from, created_at, created_by, updated_at, updated_by)
        SELECT v.id::uuid, f.id, v.version_no, v.state, v.effective_from, now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}'
        FROM master_data_config.configuration_family f,
             (VALUES ('{PublishedVersion}', 1, 'PUBLISHED', now() - interval '1 day'), ('{DraftVersion}', 2, 'DRAFT', now() - interval '1 hour')) AS v (id, version_no, state, effective_from)
        WHERE f.code = 'FIELD_CLASSIFICATION';

        INSERT INTO master_data_config.field_classification_rule (id, configuration_version_id, entity_code, field_code, data_classification_item_id, masking_rule, created_at, created_by, updated_at, updated_by)
        VALUES (gen_random_uuid(), '{PublishedVersion}', '{Entity}', 'budget', '{HighLevel}', 'WITHHOLD', now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}'),
               (gen_random_uuid(), '{DraftVersion}', '{Entity}', 'title', '{HighLevel}', 'WITHHOLD', now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}');
        """;
}
