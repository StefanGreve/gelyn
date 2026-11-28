namespace Gelyn.Core.Internals.Primitives;

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
}
