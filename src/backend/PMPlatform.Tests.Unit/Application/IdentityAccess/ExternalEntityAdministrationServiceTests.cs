using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.IdentityAccess;
using static PMPlatform.Tests.Unit.Application.IdentityAccess.AdministrationFixture;

namespace PMPlatform.Tests.Unit.Application.IdentityAccess;

/// <summary>ADM-013 (TASK-031, ADR-013): ACTIVE ↔ SUSPENDED, either → RETIRED, and RETIRED takes no write.</summary>
public sealed class ExternalEntityAdministrationServiceTests
{
    private readonly AdministrationFixture _fixture = new();

    [Fact]
    public async Task AnEntityIsSuspendedReactivatedAndRetired()
    {
        AdministrationResult<Versioned<ExternalEntityDetail>> suspended = await _fixture.EntityService().SuspendAsync(AdministratorId, EntityId, null, CancellationToken.None);
        AdministrationResult<Versioned<ExternalEntityDetail>> suspendedAgain = await _fixture.EntityService().SuspendAsync(AdministratorId, EntityId, null, CancellationToken.None);
        AdministrationResult<Versioned<ExternalEntityDetail>> active = await _fixture.EntityService().ActivateAsync(AdministratorId, EntityId, null, CancellationToken.None);
        AdministrationResult<Versioned<ExternalEntityDetail>> activeAgain = await _fixture.EntityService().ActivateAsync(AdministratorId, EntityId, null, CancellationToken.None);
        AdministrationResult<Versioned<ExternalEntityDetail>> retired = await _fixture.EntityService().RetireAsync(AdministratorId, EntityId, null, CancellationToken.None);

        Assert.Equal(ExternalEntityStatus.Suspended, suspended.Value!.Value.Status);
        Assert.Equal(AdministrationErrorKind.InvalidTransition, suspendedAgain.Error!.Kind);
        Assert.Equal(ExternalEntityStatus.Active, active.Value!.Value.Status);
        Assert.Equal(AdministrationErrorKind.InvalidTransition, activeAgain.Error!.Kind);
        Assert.Equal(ExternalEntityStatus.Retired, retired.Value!.Value.Status);
    }

    [Fact]
    public async Task ARetiredEntityTakesNoWrite()
    {
        ExternalEntity entity = _fixture.Entities.Rows[RetiredEntityId];
        ExternalEntityChanges changes = new(Label("Renamed"), EntityTypeItemId, null);

        AdministrationError?[] errors =
        [
            (await _fixture.EntityService().UpdateAsync(AdministratorId, RetiredEntityId, changes, _fixture.Entities.Versions[RetiredEntityId], CancellationToken.None)).Error,
            (await _fixture.EntityService().ActivateAsync(AdministratorId, RetiredEntityId, null, CancellationToken.None)).Error,
            (await _fixture.EntityService().SuspendAsync(AdministratorId, RetiredEntityId, null, CancellationToken.None)).Error,
            (await _fixture.EntityService().RetireAsync(AdministratorId, RetiredEntityId, null, CancellationToken.None)).Error,
        ];

        Assert.All(errors, error => Assert.Equal(AdministrationErrorKind.TerminalState, error!.Kind));
        Assert.Equal("C", entity.Name.En);
    }

    [Fact]
    public async Task TheEntityTypeAndSponsorMustBeUsable()
    {
        Guid externalUserId = Guid.Parse("00000000-0000-4000-8000-00000000a102");
        _fixture.Users.Put(External(externalUserId));

        AdministrationResult<Versioned<ExternalEntityDetail>> result = await _fixture.EntityService().CreateAsync(
            AdministratorId, new ExternalEntityDraft("ENT-D", Label("D"), Guid.NewGuid(), externalUserId), CancellationToken.None);

        Assert.Equal(IdentityAccessErrorCodes.ReferenceInvalid, result.Error!.Code);
        Assert.Equal([new FieldIssue("entityTypeItemId", FieldIssue.NotFound), new FieldIssue("sponsorUserId", FieldIssue.Inactive)], result.Error.Fields);
    }

    [Fact]
    public async Task ANewEntityIsActive()
    {
        AdministrationResult<Versioned<ExternalEntityDetail>> result = await _fixture.EntityService().CreateAsync(
            AdministratorId, new ExternalEntityDraft("ENT-D", Label("D"), EntityTypeItemId, AdministratorId), CancellationToken.None);

        Assert.Equal((ExternalEntityStatus.Active, AdministratorId), (result.Value!.Value.Status, result.Value.Value.SponsorUserId));
    }
}
