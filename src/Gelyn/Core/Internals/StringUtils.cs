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
    /// <example>
    ///     <code>
    ///     // "https://example.com/blog/", as does the same value ending on one slash or on several
    ///     EnsureEndsOn("https://example.com/blog", '/');
    ///     </code>
    /// </example>
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

            return new string(buffer[..length]);
        }
        finally
        {
            if (rented is not null) ArrayPool<char>.Shared.Return(rented);
        }
    }

    /// <summary>
    ///     Indicates whether a value begins with a prefix that a separator follows.
    /// </summary>
    /// <remarks>
    ///     Only an ordinal comparison is meaningful; a culture-aware one may report a match that is not one.
    /// </remarks>
    /// <param name="value">
    ///     The value to test. An empty span begins with nothing, the empty prefix included.
    /// </param>
    /// <param name="prefix">
    ///     The prefix to look for, with any trailing run of <paramref name="separator"/> disregarded. An empty
    ///     prefix reduces the test to <paramref name="separator"/> alone.
    /// </param>
    /// <param name="separator">
    ///     The character that has to follow the prefix.
    /// </param>
    /// <param name="comparison">
    ///     The ordinal comparison the prefix is tested under.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> when <paramref name="value"/> begins with <paramref name="prefix"/> and
    ///     carries <paramref name="separator"/> at the position the prefix ends on, <see langword="false"/>
    ///     otherwise.
    /// </returns>
    /// <example>
    ///     <code>
    ///     // true, where "blogs/post.html" against the same prefix is false
    ///     StartsWithSegment("blog/post.html", "blog", '/', StringComparison.Ordinal);
    ///     </code>
    /// </example>
    internal static bool StartsWithSegment(
        ReadOnlySpan<char> value,
        ReadOnlySpan<char> prefix,
        char separator,
        StringComparison comparison)
    {
        // The separator is tested where it would fall instead of being appended to the prefix first, which
        // is what leaves the test allocating nothing.
        ReadOnlySpan<char> body = prefix.TrimEnd(separator);

        return value.Length > body.Length
            && value[body.Length] == separator
            && value[..body.Length].Equals(body, comparison);
    }

    /// <summary>
    ///     Joins a prefix, a run of segments and a final segment, separating each from the one before it.
    /// </summary>
    /// <remarks>
    ///     The prefix is taken verbatim, so one already ending on the separator yields a doubled separator.
    /// </remarks>
    /// <param name="prefix">
    ///     The value the result opens with. An empty span yields a result opening with the separator.
    /// </param>
    /// <param name="segments">
    ///     The segments between the prefix and <paramref name="last"/>, outermost first. An empty list leaves
    ///     the prefix and <paramref name="last"/> adjacent.
    /// </param>
    /// <param name="last">
    ///     The segment the result ends on.
    /// </param>
    /// <param name="separator">
    ///     The character written before each segment.
    /// </param>
    /// <returns>
    ///     The joined value.
    /// </returns>
    /// <example>
    ///     <code>
    ///     // "/gelyn/blog/post.html"
    ///     JoinSegments("/gelyn", ["blog"], "post.html", '/');
    ///     </code>
    /// </example>
    internal static string JoinSegments(
        ReadOnlySpan<char> prefix,
        IReadOnlyList<string> segments,
        string last,
        char separator)
    {
        // Summed up front so that the result is written into one buffer, where joining the segments and then
        // interpolating the prefix onto them allocates the join only to copy it and drop it.
        int length = prefix.Length + last.Length + 1;

        for (int index = 0; index < segments.Count; index++)
            length += segments[index].Length + 1;

        char[]? rented = null;

        Span<char> buffer = length <= StackLimit
            ? stackalloc char[StackLimit]
            : (rented = ArrayPool<char>.Shared.Rent(length));

        try
        {
            prefix.CopyTo(buffer);
            int position = prefix.Length;

            for (int index = 0; index < segments.Count; index++)
            {
                string segment = segments[index];

                buffer[position++] = separator;
                segment.CopyTo(buffer[position..]);
                position += segment.Length;
            }

            buffer[position] = separator;
            last.CopyTo(buffer[(position + 1)..]);

            return new string(buffer[..length]);
        }
        finally
        {
            if (rented is not null) ArrayPool<char>.Shared.Return(rented);
        }
    }

    /// <summary>
    ///     Folds every carriage return, alone or paired with a line feed, into <see cref="Tool.LineEnding"/>.
    /// </summary>
    /// <remarks>
    ///     Narrower on purpose than <see cref="string.ReplaceLineEndings(string)"/>, which also folds the next
    ///     line, line separator, paragraph separator and form feed characters, all of which are content to HTML.
    ///     The result is written straight into its own buffer through <see cref="string.Create{TState}"/>, so a
    ///     text holding a carriage return costs one allocation and a text holding none costs nothing, as the
    ///     same instance is returned.
    /// </remarks>
    /// <param name="text">
    ///     The text to normalize.
    /// </param>
    /// <returns>
    ///     The text with line feeds as its only line breaks, which is <paramref name="text"/> itself when it
    ///     already had none other.
    /// </returns>
    /// <example>
    ///     <code>
    ///     // "a\nb\nc\n", where "a\nb" is returned as the same instance
    ///     NormalizeLineEndings("a\r\nb\rc\r\n");
    ///     </code>
    /// </example>
    internal static string NormalizeLineEndings(string text)
    {
        int first = text.IndexOf('\r');

        if (first < 0) return text;

        // Only a pair shortens the result, since a lone carriage return is replaced one for one.
        int pairs = 0;

        for (int index = first; index >= 0;)
        {
            if (index + 1 < text.Length && text[index + 1] == '\n') pairs++;

            int next = text.AsSpan(index + 1).IndexOf('\r');
            index = next < 0 ? -1 : index + 1 + next;
        }

        return string.Create(text.Length - pairs, text, static (buffer, source) =>
        {
            ReadOnlySpan<char> remaining = source;
            int position = 0;

            while (true)
            {
                int cr = remaining.IndexOf('\r');

                if (cr < 0)
                {
                    remaining.CopyTo(buffer[position..]);
                    return;
                }

                remaining[..cr].CopyTo(buffer[position..]);
                position += cr;
                remaining = remaining[(cr + 1)..];

                // A following line feed is copied by the next run, so the pair collapses onto it.
                if (remaining.Length == 0 || remaining[0] != '\n') buffer[position++] = '\n';
            }
        });
    }

    /// <summary>
    ///     Removes a delimiter from the start of a text and from its last line, along with the whitespace
    ///     surrounding the whole.
    /// </summary>
    /// <remarks>
    ///     The two ends are not matched alike. A leading delimiter is removed wherever it sits, while a
    ///     trailing one is removed only when it makes up the last line, indentation allowed, so that a text
    ///     ending on the delimiter's own characters keeps them. A delimiter between the two is left where it
    ///     is.
    /// </remarks>
    /// <param name="text">
    ///     The text to trim.
    /// </param>
    /// <param name="delimiter">
    ///     The delimiter to remove, compared ordinally.
    /// </param>
    /// <returns>
    ///     The text between the delimiters, which is empty when the text holds nothing else.
    /// </returns>
    /// <example>
    ///     <code>
    ///     // "\ntitle: Home", where "title: a---" keeps its dashes
    ///     TrimDelimiters("---\ntitle: Home\n---", "---");
    ///     </code>
    /// </example>
    internal static ReadOnlySpan<char> TrimDelimiters(ReadOnlySpan<char> text, ReadOnlySpan<char> delimiter)
    {
        ReadOnlySpan<char> body = text.Trim();

        if (body.StartsWith(delimiter)) body = body[delimiter.Length..];

        // Cut at the last break and not off the end, so the indentation before a delimiter goes with it.
        int lastBreak = body.LastIndexOfAny('\n', '\r');

        if (lastBreak >= 0 && body[(lastBreak + 1)..].Trim().SequenceEqual(delimiter))
            body = body[..lastBreak];

        return body;
    }
}
