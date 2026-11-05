namespace Gelyn.Core.Internals;

/// <summary>
///     Rules for deriving the addresses a published site uses from the URL it is served from.
/// </summary>
internal static class UrlUtils
{
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
