using System.Text.Json;
using System.Text.Json.Serialization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.DocumentManagement;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Common.Validation;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Common;

namespace PMPlatform.Api.Models.Project;

/// <summary>
/// The multipart form of a project import (R3-4): the workbook in part <c>file</c>; the importer's decisions in part <c>plan</c>, as JSON —
/// <c>{"mappings":[{"dimension":"STATUS","sourceText":"أخرى","targetValue":"ACTIVE"}],"skippedRows":["AHP-000001"]}</c>; and, on submission,
/// the note in parts <c>note</c> and <c>noteLanguage</c>. A form is not bound by MVC, so every text part obeys <see cref="TextInput"/> here.
/// </summary>
internal static class ProjectImportForm
{
    public const string PlanPart = "plan";
    public const string NotePart = "note";
    public const string NoteLanguagePart = "noteLanguage";

    private const int SourceTextLength = 200;
    private const int TargetLength = 50;
    private const int NoteLength = 2000;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper, allowIntegerValues: false) },
    };

    public static (ProjectImportUpload? Upload, ProjectImportPlan? Plan, NarrativeText? Note) Read(IFormCollection form, List<FieldError> errors)
    {
        ArgumentNullException.ThrowIfNull(form);
        IFormFile? file = form.Files.GetFile(DocumentUploadForm.FilePart);
        string? fileIssue = file is null || file.Length == 0 ? FieldError.Required
            : TextInput.Violation(file.FileName) ?? (file.FileName.Length > 255 ? FieldError.MaxLength
                : !file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ? FieldError.Malformed : null);
        if (fileIssue is not null)
        {
            errors.Add(new FieldError(DocumentUploadForm.FilePart, fileIssue));
        }

        ProjectImportPlan? plan = PlanOf(form[PlanPart].ToString(), errors);
        NarrativeText? note = null;
        string text = form[NotePart].ToString();
        if (text.Length > 0)
        {
            RequestValidation.RequireText(text, NotePart, NoteLength, errors);
            Language language = RequestValidation.Language(form[NoteLanguagePart].ToString(), NoteLanguagePart, fallback: null, errors);
            note = errors.Count == 0 ? new NarrativeText(text, language) : null;
        }

        if (errors.Count > 0 || file is null)
        {
            return (null, null, null);
        }

        using MemoryStream buffer = new();
        using (Stream content = file.OpenReadStream())
        {
            content.CopyTo(buffer);
        }

        return (new ProjectImportUpload(file.FileName, buffer.ToArray()), plan, note);
    }

    private static ProjectImportPlan? PlanOf(string json, List<FieldError> errors)
    {
        if (json.Length == 0)
        {
            return ProjectImportPlan.Empty;
        }

        ProjectImportPlan? plan;
        try
        {
            plan = JsonSerializer.Deserialize<ProjectImportPlan>(json, Json);
        }
        catch (JsonException)
        {
            plan = null;
        }

        if (plan?.Mappings is null || plan.SkippedRows is null
            || plan.Mappings.Any(m => !Enum.IsDefined(m.Dimension) || m.SourceText is null || m.SourceText.Length > SourceTextLength
                                      || TextInput.Violation(m.SourceText) is not null || string.IsNullOrWhiteSpace(m.TargetValue) || m.TargetValue.Length > TargetLength)
            || plan.SkippedRows.Any(r => string.IsNullOrWhiteSpace(r) || r.Length > SourceTextLength || TextInput.Violation(r) is not null))
        {
            errors.Add(new FieldError(PlanPart, FieldError.Malformed));
            return null;
        }

        return plan;
    }
}

/// <summary>R3-4: returning a submitted batch names why, for the importer to read.</summary>
public sealed record ProjectImportReturnCommand(MasterDataConfig.NarrativeTextRequest? Reason)
{
    internal List<FieldError> Validate(out NarrativeText? reason)
    {
        List<FieldError> errors = [];
        if (Reason is null)
        {
            errors.Add(new FieldError("reason", FieldError.Required));
        }

        reason = Reason?.Validate("reason", errors);
        return errors;
    }
}
