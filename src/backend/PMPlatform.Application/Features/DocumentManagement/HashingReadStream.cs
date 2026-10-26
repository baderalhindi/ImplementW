using System.Security.Cryptography;

namespace PMPlatform.Application.Features.DocumentManagement;

/// <summary>
/// Reads another stream forward only, hashing (SHA-256) and counting what passes through, so an upload is checksummed in
/// the same pass that stores it. The inner stream stays open.
/// </summary>
internal sealed class HashingReadStream(Stream inner) : Stream
{
    private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

    public long BytesRead { get; private set; }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => BytesRead;
        set => throw new NotSupportedException();
    }

    /// <summary>Lower-case hexadecimal SHA-256 of everything read; call once, after the last read.</summary>
    public string Sha256Hex() => Convert.ToHexStringLower(_hash.GetHashAndReset());

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        int read = inner.Read(buffer);
        Observe(buffer[..read]);
        return read;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        int read = await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        Observe(buffer.Span[..read]);
        return read;
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hash.Dispose();
        }

        base.Dispose(disposing);
    }

    private void Observe(ReadOnlySpan<byte> bytes)
    {
        _hash.AppendData(bytes);
        BytesRead += bytes.Length;
    }
}
