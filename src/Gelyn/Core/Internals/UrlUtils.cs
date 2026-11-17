namespace Gelyn.Core.Internals;

/// <summary>
///     Rules for deriving the addresses a published site uses from the URL it is served from.
/// </summary>
internal static class UrlUtils
{
    /// <summary>
    ///     Converts a URL into the directory it names, which is the form a relative reference has to be resolved
    ///     against.
    /// </summary>
    /// <remarks>
    ///     Resolution replaces the last segment of a path that does not end on a slash, so <c>feed.xml</c> resolved
    ///     against <c>https://example.com/gelyn</c> addresses <c>https://example.com/feed.xml</c>.
    /// </remarks>
    /// <param name="url">
    ///     The absolute URL to convert, with or without a trailing slash.
    /// </param>
    /// <returns>
    ///     The origin of <paramref name="url"/> and the path <see cref="ResolveBasePath"/> resolves for it, ending
    ///     on exactly one slash. The query and the fragment are dropped.
    /// </returns>
    internal static Uri AsDirectory(Uri url) =>
        new($"{url.GetLeftPart(UriPartial.Authority)}{ResolveBasePath(url)}/");

    /// <summary>
    ///     Resolves the path a site is served under, which every root-relative link the build writes opens with.
    /// </summary>
    /// <remarks>
    ///     Empty segments are dropped, because a doubled slash turns a root-relative link into one that names
    ///     another host, such as <c>//docs/index.html</c>.
    /// </remarks>
    /// <param name="baseUrl">
    ///     The URL the site is served from, or <see langword="null"/> for a site served from the root of its
    ///     origin.
    /// </param>
    /// <returns>
    ///     The non-empty segments of the path of <paramref name="baseUrl"/>, each preceded by a slash, which is
    ///     empty for a site served from the root of its origin.
    /// </returns>
    internal static string ResolveBasePath(Uri? baseUrl)
    {
        if (baseUrl is null) return string.Empty;

        string path = baseUrl.AbsolutePath;
        int length = 0;

        foreach (Range range in path.AsSpan().Split('/'))
        {
            int segment = path.AsSpan()[range].Length;

            if (segment > 0) length += segment + 1;
        }

        // An absolute path opens on a slash, so the lengths match only when every other segment is non-empty.
        if (length == path.Length) return path;

        return string.Create(length, path, static (buffer, source) =>
        {
            foreach (Range range in source.AsSpan().Split('/'))
            {
                ReadOnlySpan<char> segment = source.AsSpan()[range];

                if (segment.IsEmpty) continue;

                buffer[0] = '/';
                segment.CopyTo(buffer[1..]);
                buffer = buffer[(segment.Length + 1)..];
            }
        });
    }
}
