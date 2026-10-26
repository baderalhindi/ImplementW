using PMPlatform.Application.Common.Authorization;
using PMPlatform.Domain.IdentityAccess;
using PMPlatform.Domain.MasterDataConfig;
using static PMPlatform.Tests.Unit.Application.Authorization.AuthorizationScenario;

namespace PMPlatform.Tests.Unit.Application.Authorization;

/// <summary>ADR-010 field-level restriction by audience; what the audience does not see is listed as R-20's maskedFields.</summary>
public sealed class FieldMaskTests
{
    private const string Entity = "PROJECT";
    private static readonly Guid Internal = Guid.Parse("00000000-0000-4000-8000-000000000f01");
    private static readonly Guid Sensitive = Guid.Parse("00000000-0000-4000-8000-000000000f02");

    [Fact]
    public async Task AnAudienceBelowAFieldsClassificationGetsTheFieldsRule()
    {
        AuthorizationScenario scenario = Scenario(clearance: Internal);

        FieldMask mask = await scenario.Engine.GetFieldMaskAsync(UserId, Read, Entity, CancellationToken.None);

        Assert.Equal(MaskingRule.Withhold, mask.RuleFor("registrationBudgetSar"));
        Assert.Equal(MaskingRule.Mask, mask.RuleFor("latitude"));
        Assert.Equal(MaskingRule.Reveal, mask.RuleFor("title"));
        Assert.Equal(MaskingRule.Reveal, mask.RuleFor("description"));
        Assert.Equal(["latitude", "registrationBudgetSar"], mask.MaskedFields);
    }

    [Fact]
    public async Task AClearedAudienceSeesEveryField()
    {
        AuthorizationScenario scenario = Scenario(clearance: Sensitive);

        FieldMask mask = await scenario.Engine.GetFieldMaskAsync(UserId, Read, Entity, CancellationToken.None);

        Assert.Empty(mask.MaskedFields);
        Assert.True(mask.Reveals("registrationBudgetSar"));
    }

    [Fact]
    public async Task AUserWithoutThePermissionOrDisabledSeesNoClassifiedField()
    {
        AuthorizationScenario withoutPermission = Scenario(clearance: Sensitive);
        AuthorizationScenario disabled = Scenario(clearance: Sensitive);
        disabled.Repository.Principals[UserId] = disabled.Repository.Principals[UserId] with { IsActive = false };

        FieldMask other = await withoutPermission.Engine.GetFieldMaskAsync(UserId, OtherRead, Entity, CancellationToken.None);
        FieldMask inactive = await disabled.Engine.GetFieldMaskAsync(UserId, Read, Entity, CancellationToken.None);

        // Every classified field whose rule is not Reveal, the lowest classification included.
        Assert.Equal(["latitude", "registrationBudgetSar", "title"], other.MaskedFields);
        Assert.Equal(["latitude", "registrationBudgetSar", "title"], inactive.MaskedFields);
    }

    /// <summary>The taxonomy is outstanding (UGV-01): with no field classified, nothing is masked.</summary>
    [Fact]
    public async Task AnEntityWithNoClassifiedFieldIsNotMasked()
    {
        AuthorizationScenario scenario = new AuthorizationScenario().WithUser(UserType.Internal, Grant("R02", Read, DataScope.All));

        FieldMask mask = await scenario.Engine.GetFieldMaskAsync(UserId, Read, Entity, CancellationToken.None);

        Assert.Same(FieldMask.None, mask);
    }

    private static AuthorizationScenario Scenario(Guid clearance)
    {
        AuthorizationScenario scenario = new AuthorizationScenario().WithUser(UserType.Internal, Grant("R02", Read, DataScope.All, clearanceItemId: clearance));
        scenario.Repository.ClassificationRanks[Internal] = 1;
        scenario.Repository.ClassificationRanks[Sensitive] = 2;
        scenario.Repository.FieldClassifications[Entity] =
        [
            new("registrationBudgetSar", Sensitive, MaskingRule.Withhold),
            new("latitude", Sensitive, MaskingRule.Mask),
            new("description", Sensitive, MaskingRule.Reveal),
            new("title", Internal, MaskingRule.Withhold),
        ];
        return scenario;
    }
}
