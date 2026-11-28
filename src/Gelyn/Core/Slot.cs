
using Gelyn.Core.Internals.Primitives;

namespace Gelyn.Core;

/// <summary>
///     HTML rendered once per build, as UTF-8, cut at the one position where a page writes a fragment of its own.
/// </summary>
/// <param name="Bytes">
///     The UTF-8 bytes of the block, without the fragment.
/// </param>
/// <param name="Offset">
///     The position in <paramref name="Bytes"/> at which the fragment goes.
/// </param>
/// <param name="Fragment">
///     The fragment of the page, or an empty string.
/// </param>
internal readonly record struct Slot(ReadOnlyMemory<byte> Bytes, int Offset, string Fragment)
{
    /// <summary>
    ///     The bytes before the fragment.
    /// </summary>
    public ReadOnlySpan<byte> Before => this.Bytes.Span[..this.Offset];

    /// <summary>
    ///     The bytes after the fragment.
    /// </summary>
    public ReadOnlySpan<byte> After => this.Bytes.Span[this.Offset..];

    /// <summary>
    ///     Writes the block with the fragment in its cut.
    /// </summary>
    /// <param name="buffer">
    ///     The buffer the page is composed into.
    /// </param>
    public void WriteTo(PageBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        buffer.Append(this.Before);
        buffer.Append(this.Fragment);
        buffer.Append(this.After);
    }
}
