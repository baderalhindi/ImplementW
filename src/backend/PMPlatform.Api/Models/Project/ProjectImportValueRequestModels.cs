using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Api.Models.MasterDataConfig;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Project;

namespace PMPlatform.Api.Models.Project;

/// <summary>R3-4: ask the administrators to add a value the lists lack — the list (<c>dimension</c>) and the name as the workbook writes it.</summary>
public sealed record ProjectImportValueRequestRequest(string? Dimension, string? SourceText)
{
    private const int SourceTextLength = 200;

    internal List<FieldError> Validate(out ProjectImportDimension? dimension)
    {
        List<FieldError> errors = [];
        IReadOnlyCollection<ProjectImportDimension> dimensions = QueryParameters.EnumSet<ProjectImportDimension>(Dimension, "dimension", errors);
        if (Dimension is null || dimensions.Count != 1)
        {
            errors.Add(new FieldError("dimension", FieldError.Required));
        }

        RequestValidation.RequireText(SourceText, "sourceText", SourceTextLength, errors);
        dimension = errors.Count == 0 ? dimensions.First() : null;
        return errors;
    }
}

/// <summary>R3-4: an administrator marks a request ADDED or DECLINED, with an optional note the requester reads.</summary>
public sealed record ProjectImportValueResolveCommand(string? Status, NarrativeTextRequest? Note = null)
{
    internal List<FieldError> Validate(out ProjectImportValueRequestStatus? status, out NarrativeText? note)
    {
        List<FieldError> errors = [];
        status = Status switch
        {
            "ADDED" => ProjectImportValueRequestStatus.Added,
            "DECLINED" => ProjectImportValueRequestStatus.Declined,
            _ => null,
        };
        if (status is null)
        {
            errors.Add(new FieldError("status", Status is null ? FieldError.Required : FieldError.EnumValue));
        }

        note = Note?.Validate("note", errors);
        return errors;
    }
}
