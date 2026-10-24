using PMPlatform.Application.Common.Events;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.Project.Contracts.Events;

namespace PMPlatform.Application.Features.Project.EventHandlers;

/// <summary>
/// The one consumer of <see cref="ProjectIntakeRecorded"/> (EV-6), fanning it out to every module that writes a fact from it, in
/// <see cref="IProjectIntakeHandler.Order"/>. The outbox dispatcher runs this in the transaction that marks the message dispatched, so the
/// Declared Baseline, the opening position and the opening spend commit with the mark, once; a handler that throws undoes them all.
/// </summary>
internal sealed class ProjectIntakeDispatch(IEnumerable<IProjectIntakeHandler> handlers) : IDomainEventConsumer
{
    public string EventType => ProjectIntakeRecorded.Type;

    public async Task HandleAsync(string payload, CancellationToken cancellationToken)
    {
        ProjectIntakeRecorded intake = EventSerialization.Deserialize<ProjectIntakeRecorded>(payload);
        foreach (IProjectIntakeHandler handler in handlers.OrderBy(h => h.Order))
        {
            await handler.HandleAsync(intake, cancellationToken).ConfigureAwait(false);
        }
    }
}
