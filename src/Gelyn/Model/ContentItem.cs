namespace Gelyn.Model;

/// <summary>
///     A content file the scan has located, before its contents are read.
/// </summary>
public sealed record ContentItem
{
    /// <summary>
    ///     The absolute path of the file on disk.
    /// </summary>
    public required string SourcePath { get; init; }

    /// <summary>
    ///     The path of the generated file, relative to the output root, using the native directory separator.
    /// </summary>
    /// <seealso cref="Options.SiteOptions.OutputDirectory"/>
    public required string OutputPath { get; init; }

    /// <summary>
    ///     The root-relative URL the file is served at, including the path of the configured base URL.
    /// </summary>
    /// <seealso cref="Options.SiteOptions.BaseUrl"/>
    public required string Href { get; init; }

    /// <summary>
    ///     The title to fall back to when the front matter declares none: the file name, or the name of the
    ///     containing folder when the file is a section index.
    /// </summary>
    public required string FallbackTitle { get; init; }

    /// <summary>
    ///     Indicates whether the site navigation links the page. The landing page, root-level pages and
    ///     section indexes are linked; pages inside a section are not.
    /// </summary>
    public required bool InNavigation { get; init; }
}
