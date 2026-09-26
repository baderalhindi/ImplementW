namespace PMPlatform.Tests.Integration.Identity;

/// <summary>The system clock plus an offset, so a test can move the API past a token's expiry without waiting for it.</summary>
public sealed class AdjustableTimeProvider : TimeProvider
{
    private TimeSpan _offset;

    public override DateTimeOffset GetUtcNow() => System.GetUtcNow() + _offset;

    public void Advance(TimeSpan by) => _offset += by;

    public void Reset() => _offset = TimeSpan.Zero;
}
