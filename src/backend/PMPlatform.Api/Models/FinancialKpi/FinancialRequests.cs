using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Api.Models.MasterDataConfig;
using PMPlatform.Application.Features.FinancialKpi.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.FinancialKpi;

namespace PMPlatform.Api.Models.FinancialKpi;

/// <summary>What the WF-14 requests share: R-16 SAR amounts as decimal strings, never negative; required values; the provenance reference.</summary>
internal static class FinancialKpiFields
{
    /// <summary>Null when absent; an error when malformed or negative.</summary>
    public static Money? Optional(string? value, string field, List<FieldError> errors)
    {
        if (value is null)
        {
            return null;
        }

        Money? amount = MoneySar.Parse(value);
        if (amount is null)
        {
            errors.Add(new FieldError(field, FieldError.Malformed));
        }
        else if (amount.Value.Amount < 0)
        {
            errors.Add(new FieldError(field, FieldError.OutOfRange));
        }

        return amount;
    }

    public static Money? Required(string? value, string field, List<FieldError> errors)
    {
        if (value is null)
        {
            errors.Add(new FieldError(field, FieldError.Required));
        }

        return Optional(value, field, errors);
    }

    /// <summary>A source system's reference, or a manual document reference: up to 200 characters (ERD).</summary>
    public static void SourceReference(string? value, List<FieldError> errors) => RequestValidation.Optional(value, "sourceReference", 200, errors);

    public static void Require<T>(T? value, string field, List<FieldError> errors)
        where T : struct
    {
        if (value is null)
        {
            errors.Add(new FieldError(field, FieldError.Required));
        }
    }
}

/// <summary>Sets a financial field's source mode for the first time (ADR-008).</summary>
public sealed record FinancialSourceModeCreateRequest(Guid? ProjectId, FinancialField? FieldCode, SourceMode? SourceMode)
{
    internal List<FieldError> Validate(out FinancialSourceModeDraft? draft)
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(ProjectId, "projectId", errors);
        FinancialKpiFields.Require(FieldCode, "fieldCode", errors);
        FinancialKpiFields.Require(SourceMode, "sourceMode", errors);
        draft = errors.Count == 0 ? new FinancialSourceModeDraft(ProjectId!.Value, FieldCode!.Value, SourceMode!.Value) : null;
        return errors;
    }
}

/// <summary>A field's source mode, as a whole (R-5).</summary>
public sealed record FinancialSourceModeRequest(SourceMode? SourceMode)
{
    internal List<FieldError> Validate()
    {
        List<FieldError> errors = [];
        FinancialKpiFields.Require(SourceMode, "sourceMode", errors);
        return errors;
    }
}

/// <summary>
/// An Approved Budget version's figures, as a whole (R-5): the project total in SAR (R-16), when it applies from, and its manual
/// provenance — a document or system reference and the date the figure is true as of. Who entered it is the caller; the status,
/// version and revision are the workflow's.
/// </summary>
public sealed record FinancialCommitmentRequest(string? AmountSar, DateOnly? EffectiveFrom, string? SourceReference, DateOnly? AsOfDate)
{
    internal List<FieldError> Validate(out FinancialCommitmentChanges? changes)
    {
        List<FieldError> errors = [];
        Money? amount = FinancialKpiFields.Required(AmountSar, "amountSar", errors);
        FinancialKpiFields.SourceReference(SourceReference, errors);
        FinancialKpiFields.Require(AsOfDate, "asOfDate", errors);
        changes = errors.Count == 0 ? new FinancialCommitmentChanges(amount!.Value, EffectiveFrom, SourceReference, AsOfDate!.Value) : null;
        return errors;
    }
}

/// <summary>A new Approved Budget version of the project: <see cref="FinancialCommitmentRequest"/> and the project.</summary>
public sealed record FinancialCommitmentCreateRequest(Guid? ProjectId, string? AmountSar, DateOnly? EffectiveFrom, string? SourceReference, DateOnly? AsOfDate)
{
    internal List<FieldError> Validate(out FinancialCommitmentDraft? draft)
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(ProjectId, "projectId", errors);
        errors.AddRange(new FinancialCommitmentRequest(AmountSar, EffectiveFrom, SourceReference, AsOfDate).Validate(out FinancialCommitmentChanges? changes));
        draft = errors.Count == 0 ? new FinancialCommitmentDraft(ProjectId!.Value, changes!.AmountSar, EffectiveFrom, SourceReference, changes.AsOfDate) : null;
        return errors;
    }
}

/// <summary>A document to attach as a version's reference: the document, the version of it to pin, and its EVIDENCE_TYPE item.</summary>
public sealed record CommitmentDocumentRequest(Guid? DocumentId, Guid? DocumentVersionId, Guid? EvidenceTypeItemId)
{
    internal List<FieldError> Validate(out CommitmentDocumentAttachment? attachment)
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(DocumentId, "documentId", errors);
        RequestValidation.RequireId(DocumentVersionId, "documentVersionId", errors);
        RequestValidation.RequireId(EvidenceTypeItemId, "evidenceTypeItemId", errors);
        attachment = errors.Count == 0 ? new CommitmentDocumentAttachment(DocumentId!.Value, DocumentVersionId!.Value, EvidenceTypeItemId!.Value) : null;
        return errors;
    }
}

/// <summary>Opens the financial update of the project's next period. Every figure starts Unknown; nothing else is sent.</summary>
public sealed record FinancialProgressUpdateStartRequest(Guid? ProjectId)
{
    internal List<FieldError> Validate()
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(ProjectId, "projectId", errors);
        return errors;
    }
}

/// <summary>
/// A DRAFT update's figures, as a whole (R-5). A figure that is not known is sent as null, with <c>valueStatus</c> saying why
/// (MISSING, STALE, NOT_APPLICABLE) — never as <c>"0.00"</c>. A figure is present exactly when MEASURED.
/// </summary>
public sealed record FinancialProgressUpdateRequest(
    string? ActualExpenditureToDateSar, string? ForecastAtCompletionSar, ValueStatus? ValueStatus, NarrativeTextRequest? Narrative, string? SourceReference, DateOnly? AsOfDate)
{
    internal List<FieldError> Validate(out FinancialProgressUpdateChanges? changes)
    {
        List<FieldError> errors = [];
        Money? actual = FinancialKpiFields.Optional(ActualExpenditureToDateSar, "actualExpenditureToDateSar", errors);
        Money? forecast = FinancialKpiFields.Optional(ForecastAtCompletionSar, "forecastAtCompletionSar", errors);
        FinancialKpiFields.Require(ValueStatus, "valueStatus", errors);
        NarrativeText? narrative = Narrative?.Validate("narrative", errors);
        FinancialKpiFields.SourceReference(SourceReference, errors);
        FinancialKpiFields.Require(AsOfDate, "asOfDate", errors);
        changes = errors.Count == 0 ? new FinancialProgressUpdateChanges(actual, forecast, ValueStatus!.Value, narrative, SourceReference, AsOfDate!.Value) : null;
        return errors;
    }
}
