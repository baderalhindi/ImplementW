using PMPlatform.Application.Features.IdentityAccess.Administration;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.IdentityAccess;
using static PMPlatform.Tests.Unit.Application.IdentityAccess.AdministrationFixture;

namespace PMPlatform.Tests.Unit.Application.IdentityAccess;

/// <summary>ADM-011/012 (TASK-031): the department tree has no cycle, and sign-in's directory references stay unambiguous.</summary>
public sealed class DepartmentAdministrationServiceTests
{
    private static readonly Guid ChildId = Guid.Parse("00000000-0000-4000-8000-00000000b011");
    private static readonly Guid GrandchildId = Guid.Parse("00000000-0000-4000-8000-00000000b012");

    private readonly AdministrationFixture _fixture = new();

    public DepartmentAdministrationServiceTests()
    {
        _fixture.Departments.Put(new Department { Id = ChildId, Code = "DEPT-A1", Name = Label("A1"), ParentDepartmentId = AdministrationFixture.DepartmentId });
        _fixture.Departments.Put(new Department { Id = GrandchildId, Code = "DEPT-A11", Name = Label("A11"), ParentDepartmentId = ChildId });
    }

    [Theory]
    [InlineData("00000000-0000-4000-8000-00000000b001", true)]
    [InlineData("00000000-0000-4000-8000-00000000b011", true)]
    [InlineData("00000000-0000-4000-8000-00000000b012", true)]
    [InlineData("00000000-0000-4000-8000-00000000b002", false)]
    public async Task ADepartmentCannotMoveUnderItselfOrItsDescendants(string parentId, bool cycle)
    {
        Department department = _fixture.Departments.Rows[AdministrationFixture.DepartmentId];

        AdministrationResult<Versioned<DepartmentDetail>> result = await _fixture.DepartmentService().UpdateAsync(
            AdministratorId, department.Id, new DepartmentChanges(department.Name, Guid.Parse(parentId), department.DirectoryReference),
            _fixture.Departments.Versions[department.Id], CancellationToken.None);

        Assert.Equal(cycle ? IdentityAccessErrorCodes.DepartmentCycle : null, result.Error?.Code);
    }

    [Fact]
    public void AnAncestryThatAlreadyLoopsCountsAsACycle() =>
        Assert.True(DepartmentAdministrationService.IsOwnAncestor(
            Guid.NewGuid(), ChildId, new Dictionary<Guid, Guid?> { [ChildId] = GrandchildId, [GrandchildId] = ChildId }));

    /// <summary>ADR-007: sign-in resolves a department by its directory reference, so two active departments never share one.</summary>
    [Fact]
    public async Task NoTwoActiveDepartmentsShareADirectoryReference()
    {
        AdministrationResult<Versioned<DepartmentDetail>> create = await _fixture.DepartmentService().CreateAsync(
            AdministratorId, new DepartmentDraft("DEPT-C", Label("C"), null, "OU-A"), CancellationToken.None);
        await _fixture.DepartmentService().DeactivateAsync(AdministratorId, AdministrationFixture.DepartmentId, null, CancellationToken.None);
        AdministrationResult<Versioned<DepartmentDetail>> afterDeactivation = await _fixture.DepartmentService().CreateAsync(
            AdministratorId, new DepartmentDraft("DEPT-C", Label("C"), null, "OU-A"), CancellationToken.None);
        AdministrationResult<Versioned<DepartmentDetail>> reactivate = await _fixture.DepartmentService().ActivateAsync(
            AdministratorId, AdministrationFixture.DepartmentId, null, CancellationToken.None);

        Assert.Equal([new FieldIssue("directoryReference", FieldIssue.Duplicate)], create.Error!.Fields);
        Assert.True(afterDeactivation.Succeeded);
        Assert.Equal(IdentityAccessErrorCodes.DuplicateKey, reactivate.Error!.Code);
    }

    [Fact]
    public async Task DeactivationIsAOneWayStepUntilActivated()
    {
        AdministrationResult<Versioned<DepartmentDetail>> first = await _fixture.DepartmentService().DeactivateAsync(AdministratorId, ChildId, null, CancellationToken.None);
        AdministrationResult<Versioned<DepartmentDetail>> second = await _fixture.DepartmentService().DeactivateAsync(AdministratorId, ChildId, null, CancellationToken.None);

        Assert.False(first.Value!.Value.IsActive);
        Assert.Equal(AdministrationErrorKind.InvalidTransition, second.Error!.Kind);
        Assert.Equal(GrandchildId, _fixture.Departments.Rows[GrandchildId].Id);
        Assert.Equal(ChildId, _fixture.Departments.Rows[GrandchildId].ParentDepartmentId);
    }

    [Fact]
    public async Task ANewDepartmentGoesUnderAnActiveParent()
    {
        await _fixture.DepartmentService().DeactivateAsync(AdministratorId, ChildId, null, CancellationToken.None);

        AdministrationResult<Versioned<DepartmentDetail>> underInactive = await _fixture.DepartmentService().CreateAsync(
            AdministratorId, new DepartmentDraft("DEPT-D", Label("D"), ChildId, null), CancellationToken.None);
        AdministrationResult<Versioned<DepartmentDetail>> underMissing = await _fixture.DepartmentService().CreateAsync(
            AdministratorId, new DepartmentDraft("DEPT-D", Label("D"), Guid.NewGuid(), null), CancellationToken.None);

        Assert.Equal([new FieldIssue("parentDepartmentId", FieldIssue.Inactive)], underInactive.Error!.Fields);
        Assert.Equal([new FieldIssue("parentDepartmentId", FieldIssue.NotFound)], underMissing.Error!.Fields);
    }
}
