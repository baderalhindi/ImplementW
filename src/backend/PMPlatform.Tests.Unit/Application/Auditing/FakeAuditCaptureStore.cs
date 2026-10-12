using PMPlatform.Application.Features.AuditActivity;

namespace PMPlatform.Tests.Unit.Application.Auditing;

internal sealed class FakeAuditCaptureStore : IAuditCaptureStore
{
    public List<AuditCapture> Staged { get; } = [];

    public List<AuditCapture> Recorded { get; } = [];

    public void Stage(AuditCapture capture) => Staged.Add(capture);

    public Task RecordAsync(AuditCapture capture)
    {
        Recorded.Add(capture);
        return Task.CompletedTask;
    }
}

internal sealed class FixedRequestContext(Guid correlationId, string? clientAddress = null, Guid? userId = null) : PMPlatform.Application.Common.Auditing.IAuditRequestContext
{
    public Guid CorrelationId { get; } = correlationId;

    public string? ClientAddress { get; } = clientAddress;

    public Guid? UserId { get; } = userId;
}
