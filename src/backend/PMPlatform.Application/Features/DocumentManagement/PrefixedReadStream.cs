namespace PMPlatform.Application.Features.DocumentManagement;

/// <summary>
/// Reads <paramref name="prefix"/>, then the rest of <paramref name="inner"/>, forward only: an upload whose leading bytes
/// were read to check its type is stored whole, without seeking a stream that may not seek. The inner stream stays open.
/// </summary>
internal sealed class PrefixedReadStream(ReadOnlyMemory<byte> prefix, Stream inner) : Stream
{
    private ReadOnlyMemory<byte> _prefix = prefix;

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        if (_prefix.IsEmpty || buffer.IsEmpty)
        {
            return inner.Read(buffer);
        }

        int copied = Math.Min(buffer.Length, _prefix.Length);
        _prefix.Span[..copied].CopyTo(buffer);
        _prefix = _prefix[copied..];
        return copied;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        _prefix.IsEmpty || buffer.IsEmpty ? inner.ReadAsync(buffer, cancellationToken) : ValueTask.FromResult(Read(buffer.Span));

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
