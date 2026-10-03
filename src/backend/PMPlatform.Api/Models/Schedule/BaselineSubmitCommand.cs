using PMPlatform.Application.Features.Schedule.Contracts;

namespace PMPlatform.Api.Models.Schedule;

/// <summary>Submitting a candidate. A rebaseline names the approved WF-08 change authorisation it implements (BR-SCH-034).</summary>
public sealed record BaselineSubmitCommand(Guid? ChangeAuthorizationId)
{
    internal BaselineSubmission ToSubmission() => new(ChangeAuthorizationId);
}
