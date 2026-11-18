namespace Gelyn.Model;

/// <summary>
///     A document the build writes that no source file of its own stands behind, such as the feed, the sitemap
///     or a file of the theme.
/// </summary>
internal sealed record GeneratedDocument
{
    /// <summary>
    ///     The output-relative path the document is written to.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    ///     The bytes the document consists of.
    /// </summary>
    public required ReadOnlyMemory<byte> Contents { get; init; }
}
