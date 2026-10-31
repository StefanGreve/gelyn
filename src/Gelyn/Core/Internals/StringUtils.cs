using System.Buffers;

namespace Gelyn.Core.Internals;

/// <summary>
///     Operations on text that the framework offers in no single call.
/// </summary>
internal static class StringUtils
{
    // 512 bytes, which is past the longest path or URL this is handed and short enough to leave the stack be.
    // A constant rather than the requested length, a stackalloc sized at runtime being the one that overflows.
    private const int StackLimit = 256;

    /// <summary>
    ///     Terminates a value with exactly one occurrence of a character, appending one where the value ends
    ///     on none and collapsing the run where it ends on several.
    /// </summary>
    /// <remarks>
    ///     The result is built in one pass over a buffer, where <c>$"{value.TrimEnd(c)}{c}"</c> allocates the
    ///     trimmed value and then the interpolated result. Taking a span also lets a caller pass a slice of
    ///     something larger without materializing that slice as a string of its own first.
    /// </remarks>
    /// <param name="value">
    ///     The value to terminate. An empty span yields the terminator on its own.
    /// </param>
    /// <param name="terminator">
    ///     The character the result ends on.
    /// </param>
    /// <returns>
    ///     The terminated value.
    /// </returns>
    internal static string EnsureEndsOn(ReadOnlySpan<char> value, char terminator)
    {
        ReadOnlySpan<char> body = value.TrimEnd(terminator);
        int length = body.Length + 1;

        char[]? rented = null;

        Span<char> buffer = length <= StackLimit
            ? stackalloc char[StackLimit]
            : (rented = ArrayPool<char>.Shared.Rent(length));

        try
        {
            body.CopyTo(buffer);
            buffer[body.Length] = terminator;

            // Sliced because Rent answers with at least the requested length and often more.
            return new string(buffer[..length]);
        }
        finally
        {
            if (rented is not null) ArrayPool<char>.Shared.Return(rented);
        }
    }
}
