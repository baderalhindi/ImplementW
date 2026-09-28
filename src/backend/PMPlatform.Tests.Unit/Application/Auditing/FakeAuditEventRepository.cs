using PMPlatform.Application.Features.AuditActivity;

namespace PMPlatform.Tests.Unit.Application.Auditing;

internal sealed class FakeAuditEventRepository : IAuditEventRepository
{
    public List<AuditRecord> Staged { get; } = [];

    public List<AuditRecord> Appended { get; } = [];

    public void Stage(AuditRecord record) => Staged.Add(record);

    public Task AppendAsync(AuditRecord record)
    {
        Appended.Add(record);
        return Task.CompletedTask;
    }
}

internal sealed class FixedRequestContext(Guid correlationId, string? clientAddress = null, Guid? userId = null) : PMPlatform.Application.Common.Auditing.IAuditRequestContext
{
    public Guid CorrelationId { get; } = correlationId;

    public string? ClientAddress { get; } = clientAddress;

    public Guid? UserId { get; } = userId;
}
