using PMPlatform.Application.Features.Approval;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Domain.Common;

namespace PMPlatform.Tests.Unit.Application.Approval;

/// <summary>TASK-035 routing over the APPROVAL_AUTHORITY matrix: applicable rows are the stages; nothing is guessed.</summary>
public sealed class ApprovalRoutingTests
{
    private static readonly Guid Standard = Guid.Parse("00000000-0000-4000-8000-00000000e001");
    private static readonly Guid Full = Guid.Parse("00000000-0000-4000-8000-00000000e002");
    private static readonly Guid DepartmentManager = Guid.Parse("00000000-0000-4000-8000-000000000003");
    private static readonly Guid PortfolioManager = Guid.Parse("00000000-0000-4000-8000-000000000002");
    private static readonly Guid Executive = Guid.Parse("00000000-0000-4000-8000-000000000007");

    [Fact]
    public void EveryApplicableRowIsATaskInStageOrder()
    {
        ResolvedConfiguration authority = Authority(
            Row("CHANGE_REQUEST", 2, PortfolioManager, bandNo: 2),
            Row("CHANGE_REQUEST", 1, DepartmentManager),
            Row("CHANGE_REQUEST", 3, Executive, minAmount: 1_000_000m),
            Row("CHANGE_REQUEST", 2, Executive, profile: Full),
            Row("BASELINE", 1, Executive));

        IReadOnlyList<RoutedSeat> seats = ApprovalRouting.Route(authority, Start("CHANGE_REQUEST", Standard, bandNo: 2, amount: 250_000m));

        Assert.Equal([new RoutedSeat(1, DepartmentManager), new RoutedSeat(2, PortfolioManager)], seats);
    }

    [Fact]
    public void AnAmountAtOrAboveTheThresholdAddsItsStageAndNoAmountNeverMeetsOne()
    {
        ResolvedConfiguration authority = Authority(Row("COMMITMENT", 1, DepartmentManager), Row("COMMITMENT", 2, Executive, minAmount: 1_000_000m));

        Assert.Equal(2, ApprovalRouting.Route(authority, Start("COMMITMENT", Standard, amount: 1_000_000m)).Count);
        Assert.Single(ApprovalRouting.Route(authority, Start("COMMITMENT", Standard, amount: 999_999.99m)));
        Assert.Single(ApprovalRouting.Route(authority, Start("COMMITMENT", Standard, amount: null)));
    }

    /// <summary>Blueprint Section 12: no route is missing configuration, never an empty or default route.</summary>
    [Fact]
    public void NoApplicableRowFailsClosed()
    {
        ConfigurationMissingException missing = Assert.Throws<ConfigurationMissingException>(() =>
            ApprovalRouting.Route(Authority(Row("BASELINE", 1, Executive)), Start("CLOSURE", Standard)));

        Assert.Equal(("APPROVAL_AUTHORITY", ConfigurationMissingReason.EntryMissing), (missing.ConfigurationCode, missing.Reason));
    }

    /// <summary>ERD O-4: two applicable rows for one role at one stage are ambiguous, and are refused rather than merged.</summary>
    [Fact]
    public void TwoRowsForTheSameRoleAndStageFailClosed()
    {
        ResolvedConfiguration authority = Authority(Row("BASELINE", 1, Executive), Row("BASELINE", 1, Executive, profile: Standard));

        ConfigurationMissingException ambiguous = Assert.Throws<ConfigurationMissingException>(() => ApprovalRouting.Route(authority, Start("BASELINE", Standard)));

        Assert.Equal(ConfigurationMissingReason.EntryInvalid, ambiguous.Reason);
    }

    private static ApprovalAuthorityEntry Row(string subjectType, short sequenceNo, Guid roleId, Guid? profile = null, short? bandNo = null, decimal? minAmount = null) =>
        new(subjectType, profile, bandNo, minAmount, sequenceNo, roleId, IsMandatory: true);

    private static ResolvedConfiguration Authority(params ApprovalAuthorityEntry[] rows) =>
        new(Guid.NewGuid(), "APPROVAL_AUTHORITY", 1, DateTimeOffset.UnixEpoch, null, new ConfigurationContent { ApprovalAuthority = rows });

    private static ApprovalStart Start(string routingKey, Guid profile, short? bandNo = null, decimal? amount = null) =>
        new(new ApprovalSubject("ChangeRequest", "ChangeRequest", Guid.NewGuid(), 1), routingKey, Guid.NewGuid(), null, null, profile, bandNo,
            amount is { } value ? new Money(value) : null);
}
