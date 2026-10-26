using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.FinancialKpi.Contracts;

/// <summary>
/// ADR-008's source mode, per project and per financial field (TASK-052). MANUAL at launch; INTEGRATED and HYBRID are built and
/// left unconnected. Once a field is INTEGRATED no figure is entered for it by hand and it never returns to another mode.
/// </summary>
public interface IFinancialSourceModeService
{
    /// <summary>Every financial field of the project with its mode, MANUAL where none was set.</summary>
    public Task<FinancialSourceModePage> ListAsync(Guid callerId, Guid projectId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>A configured field's mode, with its version for <see cref="UpdateAsync"/> (R-21).</summary>
    public Task<AdministrationResult<Versioned<FinancialSourceModeDetail>>> GetAsync(Guid callerId, Guid sourceModeId, CancellationToken cancellationToken);

    /// <summary>Sets a field's mode for the first time.</summary>
    public Task<AdministrationResult<Versioned<FinancialSourceModeDetail>>> CreateAsync(Guid callerId, FinancialSourceModeDraft draft, CancellationToken cancellationToken);

    /// <summary>Changes a field's mode; an INTEGRATED field is locked (409). Requires the caller's version (R-21).</summary>
    public Task<AdministrationResult<Versioned<FinancialSourceModeDetail>>> UpdateAsync(
        Guid callerId, Guid sourceModeId, Domain.FinancialKpi.SourceMode sourceMode, uint expectedVersion, CancellationToken cancellationToken);
}
