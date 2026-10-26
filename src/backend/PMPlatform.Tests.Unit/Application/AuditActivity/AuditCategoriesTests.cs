using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.AuditActivity;
using PMPlatform.Application.Features.AuditActivity.Contracts;
using PMPlatform.Domain.AuditActivity;
using PMPlatform.Domain.Common;

namespace PMPlatform.Tests.Unit.Application.AuditActivity;

/// <summary>TASK-073 (AUD-CC-03): every event is in exactly one category, decided the same way in memory and in the database.</summary>
public sealed class AuditCategoriesTests
{
    private static readonly string[] Producers = ["IdentityAccess", "Risk", "FinancialKpi", "ExternalParticipation", "DocumentManagement", "Approval"];

    [Fact]
    public void TheQueryConditionIsThePartitionOfOf()
    {
        foreach (AuditEventClass eventClass in Enum.GetValues<AuditEventClass>())
        {
            foreach (string producer in Producers)
            {
                AuditEvent e = AuditEvents.Of($"{producer}.SomethingHappened", eventClass);
                List<AuditCategory> matched = [.. AuditCategories.All.Where(c => AuditCategories.In(c).Compile()(e))];

                Assert.Equal([AuditCategories.Of(e)], matched);
            }
        }
    }

    [Theory]
    [InlineData("IdentityAccess.SignInFailed", AuditEventClass.Authentication, AuditCategory.Security)]
    [InlineData("Risk.AuthorityRefused", AuditEventClass.AuthorizationDenial, AuditCategory.Security)]
    [InlineData("IdentityAccess.RoleAssigned", AuditEventClass.PermissionChange, AuditCategory.Administration)]
    [InlineData("MasterDataConfig.VersionPublished", AuditEventClass.ConfigurationChange, AuditCategory.Administration)]
    [InlineData("FinancialKpi.VersionActivated", AuditEventClass.LifecycleTransition, AuditCategory.Financial)]
    [InlineData("ExternalParticipation.ContributionAccepted", AuditEventClass.LifecycleTransition, AuditCategory.External)]
    [InlineData("Risk.RiskClosed", AuditEventClass.LifecycleTransition, AuditCategory.Business)]
    public void EachEventIsInItsCategory(string eventType, AuditEventClass eventClass, AuditCategory category) =>
        Assert.Equal(category, AuditCategories.Of(AuditEvents.Of(eventType, eventClass)));

    /// <summary>One permission per category, each in a group of its own, so one category never makes another's event visible (R-47).</summary>
    [Fact]
    public void EachCategoryHasItsOwnPermissionInItsOwnGroup()
    {
        List<PermissionDefinition> permissions = [.. AuditCategories.All.Select(c => PermissionCatalogue.Platform.Get(AuditCategories.PermissionOf(c)))];

        Assert.Equal(AuditCategories.All.Count, permissions.Select(p => p.Group).Distinct(StringComparer.Ordinal).Count());
        Assert.All(permissions, p => Assert.Equal(AccessMode.Read, p.Mode));
    }

    /// <summary>BR-AUD-023: R01's shipped grants open the administration category and nothing else of formal audit.</summary>
    [Fact]
    public void AdministratorsReadOnlyTheAdministrationCategory()
    {
        HashSet<string> administrator = [.. PermissionCatalogue.ShippedDefaultGrants.Where(g => g.RoleCode == "R01").Select(g => g.PermissionCode)];

        Assert.Equal([AuditCategory.Administration], AuditCategories.All.Where(c => administrator.Contains(AuditCategories.PermissionOf(c))));
        Assert.DoesNotContain(PermissionCatalogue.AuditContextView, administrator);
        Assert.Contains(PermissionCatalogue.AuditOperationsManage, administrator);
    }
}
