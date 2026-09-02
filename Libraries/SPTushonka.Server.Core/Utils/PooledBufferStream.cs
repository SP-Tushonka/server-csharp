using System.Buffers;

namespace SPTarkov.Server.Core.Utils;

/// <summary>
///     Write-only stream over a pooled array. A response is compressed into it and shuffled in place.
/// </summary>
public sealed class PooledBufferStream(int initialCapacity) : Stream
{
    private byte[] _buffer = ArrayPool<byte>.Shared.Rent(initialCapacity);
    private int _length;

    public byte[] Buffer
    {
        get { return _buffer; }
    }

    public override bool CanRead
    {
        get { return false; }
    }

    public override bool CanSeek
    {
        get { return false; }
    }

    public override bool CanWrite
    {
        get { return true; }
    }

    public override long Length
    {
        get { return _length; }
    }

    public override long Position
    {
        get { return _length; }
        set { throw new NotSupportedException(); }
    }

    /// <summary>Leave <paramref name="count" /> bytes to be filled in later, such as the length header.</summary>
    public void Reserve(int count)
    {
        EnsureCapacity(_length + count);
        _length += count;
    }

    public override void Write(ReadOnlySpan<byte> source)
    {
        EnsureCapacity(_length + source.Length);
        source.CopyTo(_buffer.AsSpan(_length));
        _length += source.Length;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        Write(buffer.AsSpan(offset, count));
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default)
    {
        Write(source.Span);

        return ValueTask.CompletedTask;
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        Write(buffer.AsSpan(offset, count));

        return Task.CompletedTask;
    }

    public override void Flush() { }

    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    private void EnsureCapacity(int needed)
    {
        if (needed <= _buffer.Length)
        {
            return;
        }

        var bigger = ArrayPool<byte>.Shared.Rent(Math.Max(needed, _buffer.Length * 2));
        _buffer.AsSpan(0, _length).CopyTo(bigger);
        ArrayPool<byte>.Shared.Return(_buffer);
        _buffer = bigger;
    }

    protected override void Dispose(bool disposing)
    {
        if (_buffer.Length > 0)
        {
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = [];
        }

        base.Dispose(disposing);
    }
}
