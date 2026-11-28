using System.Buffers;
using System.Text;

namespace Gelyn.Core.Internals.Primitives;

/// <summary>
///     A growing buffer of UTF-8 bytes, rented from the shared array pool, into which one document is composed.
/// </summary>
/// <remarks>
///     The arrays go back to the pool on disposal, so a worker that composes one page after another reuses them.
///     Text is encoded with <see cref="Tool.Encoding"/> as it is appended, so a document is never held as a
///     string.
/// </remarks>
internal sealed class PageBuffer : IBufferWriter<byte>, IDisposable
{
    private const int InitialLength = 16 * 1024;
    private const byte CarriageReturn = (byte)'\r';
    private const byte LineFeed = (byte)'\n';

    private byte[] _buffer = ArrayPool<byte>.Shared.Rent(InitialLength);
    private bool _disposed;

    /// <summary>
    ///     The number of bytes written so far.
    /// </summary>
    public int Length { get; private set; }

    /// <summary>
    ///     The bytes written so far.
    /// </summary>
    /// <exception cref="ObjectDisposedException">
    ///     The buffer has been disposed.
    /// </exception>
    public ReadOnlySpan<byte> WrittenSpan
    {
        get
        {
            ObjectDisposedException.ThrowIf(this._disposed, this);

            return this._buffer.AsSpan(0, this.Length);
        }
    }

    /// <summary>
    ///     Appends bytes as they are.
    /// </summary>
    /// <param name="bytes">
    ///     The bytes to append.
    /// </param>
    /// <exception cref="ObjectDisposedException">
    ///     The buffer has been disposed.
    /// </exception>
    public void Append(ReadOnlySpan<byte> bytes)
    {
        bytes.CopyTo(this.GetSpan(bytes.Length));
        this.Length += bytes.Length;
    }

    /// <summary>
    ///     Appends text as UTF-8.
    /// </summary>
    /// <param name="text">
    ///     The text to append.
    /// </param>
    /// <exception cref="EncoderFallbackException">
    ///     <paramref name="text"/> holds an unpaired surrogate, in which case nothing of it is appended.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    ///     The buffer has been disposed.
    /// </exception>
    public void Append(ReadOnlySpan<char> text)
    {
        ObjectDisposedException.ThrowIf(this._disposed, this);

        Tool.Encoding.GetBytes(text, this);
    }

    /// <summary>
    ///     Folds every carriage return written so far into a line feed, in place, so that the document ends every
    ///     line on <see cref="Tool.LineEnding"/> whatever the content or the configuration carried.
    /// </summary>
    /// <exception cref="ObjectDisposedException">
    ///     The buffer has been disposed.
    /// </exception>
    public void FoldLineEndings()
    {
        ObjectDisposedException.ThrowIf(this._disposed, this);

        // A pair collapses onto its line feed and a lone carriage return becomes one. Folding the raw bytes is safe,
        // because UTF-8 never uses the byte 0x0D inside a multi-byte sequence, and U+2028 and its kind are left alone.
        Span<byte> bytes = this._buffer.AsSpan(0, this.Length);
        int position = bytes.IndexOf(CarriageReturn);

        if (position < 0) return;

        Span<byte> remaining = bytes[position..];

        while (true)
        {
            int cr = remaining.IndexOf(CarriageReturn);

            if (cr < 0)
            {
                remaining.CopyTo(bytes[position..]);
                this.Length = position + remaining.Length;
                return;
            }

            remaining[..cr].CopyTo(bytes[position..]);
            position += cr;
            remaining = remaining[(cr + 1)..];

            // A following line feed is copied by the next run, so the pair collapses onto it.
            if (remaining.Length == 0 || remaining[0] != LineFeed)
                bytes[position++] = LineFeed;
        }
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">
    ///     The buffer has been disposed.
    /// </exception>
    public void Advance(int count)
    {
        ObjectDisposedException.ThrowIf(this._disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, this._buffer.Length - this.Length);

        this.Length += count;
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">
    ///     The buffer has been disposed.
    /// </exception>
    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        this.Reserve(sizeHint);
        return this._buffer.AsMemory(this.Length);
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">
    ///     The buffer has been disposed.
    /// </exception>
    public Span<byte> GetSpan(int sizeHint = 0)
    {
        this.Reserve(sizeHint);
        return this._buffer.AsSpan(this.Length);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    #region Helpers

    // Private rather than protected virtual, since the class is sealed, and never called with false, since no
    // finalizer exists: the array is managed, and the pool survives an instance that is never disposed.
    private void Dispose(bool disposing)
    {
        if (this._disposed) return;

        if (disposing)
        {
            ArrayPool<byte>.Shared.Return(this._buffer);
            this._buffer = [];
            this.Length = 0;
        }

        this._disposed = true;
    }

    // A hint of zero asks for any free space at all, as IBufferWriter<T> defines it.
    private void Reserve(int sizeHint)
    {
        ObjectDisposedException.ThrowIf(this._disposed, this);

        int needed = Math.Max(sizeHint, 1);

        if (this._buffer.Length - this.Length >= needed) return;

        byte[] larger = ArrayPool<byte>.Shared.Rent(Math.Max(this.Length + needed, this._buffer.Length * 2));

        this.WrittenSpan.CopyTo(larger);
        ArrayPool<byte>.Shared.Return(this._buffer);
        this._buffer = larger;
    }

    #endregion
}
