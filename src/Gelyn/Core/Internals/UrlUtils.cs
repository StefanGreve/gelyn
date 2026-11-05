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
    ///     The URL ending on exactly one slash.
    /// </returns>
    internal static Uri AsDirectory(Uri url) =>
        new(StringUtils.EnsureEndsOn(url.AbsoluteUri, '/'));

    /// <summary>
    ///     Resolves the path a site is served under, which every root-relative link the build writes opens with.
    /// </summary>
    /// <param name="baseUrl">
    ///     The URL the site is served from, or <see langword="null"/> for a site served from the root of its
    ///     origin.
    /// </param>
    /// <returns>
    ///     The path of <paramref name="baseUrl"/> without a trailing slash, which is empty for a site served from
    ///     the root of its origin.
    /// </returns>
    internal static string ResolveBasePath(Uri? baseUrl) =>
        baseUrl?.AbsolutePath.TrimEnd('/') ?? string.Empty;
}
