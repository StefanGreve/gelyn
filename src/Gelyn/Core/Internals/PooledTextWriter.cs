using System.Buffers;
using System.Text;

namespace Gelyn.Core.Internals;

/// <summary>
///     A text writer over a growing array of characters, rented from the shared array pool, into which one document
///     is rendered.
/// </summary>
/// <remarks>
///     The arrays go back to the pool on disposal, so a worker that renders one page after another reuses them. The
///     members of <see cref="TextWriter"/> that this class leaves alone reach its overrides through the base class,
///     so the overrides decide speed, never correctness.
/// </remarks>
internal sealed class PooledTextWriter : TextWriter
{
    private const int InitialLength = 16 * 1024;

    private char[] _buffer = ArrayPool<char>.Shared.Rent(InitialLength);
    private int _length;
    private bool _disposed;

    /// <summary>
    ///     The characters written so far.
    /// </summary>
    /// <exception cref="ObjectDisposedException">
    ///     The writer has been disposed.
    /// </exception>
    public ReadOnlySpan<char> WrittenSpan
    {
        get
        {
            ObjectDisposedException.ThrowIf(this._disposed, this);

            return this._buffer.AsSpan(0, this._length);
        }
    }

    /// <summary>
    ///     UTF-16, the encoding of the characters the writer holds, as Markdig's own writer reports it.
    /// </summary>
    public override Encoding Encoding => Encoding.Unicode;

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">
    ///     The writer has been disposed.
    /// </exception>
    public override void Write(char value)
    {
        this.Reserve(1);
        this._buffer[this._length++] = value;
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">
    ///     The writer has been disposed.
    /// </exception>
    public override void Write(string? value) => this.Write(value.AsSpan());

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">
    ///     The writer has been disposed.
    /// </exception>
    public override void Write(char[] buffer, int index, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        this.Write(buffer.AsSpan(index, count));
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">
    ///     The writer has been disposed.
    /// </exception>
    public override void Write(ReadOnlySpan<char> buffer)
    {
        this.Reserve(buffer.Length);
        buffer.CopyTo(this._buffer.AsSpan(this._length));
        this._length += buffer.Length;
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">
    ///     The writer has been disposed.
    /// </exception>
    public override void WriteLine() => this.Write(this.CoreNewLine.AsSpan());

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">
    ///     The writer has been disposed.
    /// </exception>
    public override void WriteLine(string? value)
    {
        this.Write(value);
        this.WriteLine();
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">
    ///     The writer has been disposed.
    /// </exception>
    public override void WriteLine(ReadOnlySpan<char> buffer)
    {
        this.Write(buffer);
        this.WriteLine();
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing && !this._disposed)
        {
            ArrayPool<char>.Shared.Return(this._buffer);
            this._buffer = [];
            this._length = 0;
        }

        this._disposed = true;

        base.Dispose(disposing);
    }

    #region Helpers

    private void Reserve(int needed)
    {
        ObjectDisposedException.ThrowIf(this._disposed, this);

        if (this._buffer.Length - this._length >= needed) return;

        char[] larger = ArrayPool<char>.Shared.Rent(Math.Max(this._length + needed, this._buffer.Length * 2));

        this.WrittenSpan.CopyTo(larger);
        ArrayPool<char>.Shared.Return(this._buffer);
        this._buffer = larger;
    }

    #endregion
}
