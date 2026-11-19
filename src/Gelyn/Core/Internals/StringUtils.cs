namespace Gelyn.Core.Internals;

/// <summary>
///     Operations on text that the framework offers in no single call.
/// </summary>
internal static class StringUtils
{
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
}
