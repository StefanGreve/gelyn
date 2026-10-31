namespace Gelyn.Model;

/// <summary>
///     A discovered content file, rendered and ready to be laid out.
/// </summary>
/// <remarks>
///     A <see cref="ContentItem"/> becomes a page once its Markdown has been read. The output path and the
///     URL are copied across unchanged, while the front matter and the body are added by the render.
/// </remarks>
internal sealed record ContentPage
{
    /// <summary>
    ///     The path of the generated file, relative to the output root, using the native directory separator.
    /// </summary>
    /// <seealso cref="Options.SiteOptions.OutputDirectory"/>
    public required string OutputPath { get; init; }

    /// <summary>
    ///     The root-relative URL the page is linked by, including the path of the configured base URL.
    /// </summary>
    /// <seealso cref="Options.SiteOptions.BaseUrl"/>
    public required string Href { get; init; }

    /// <summary>
    ///     The page title, falling back to the file or folder name when the front matter omits it.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    ///     The description declared in the front matter, or <see langword="null"/> to fall back to the
    ///     description configured for the site.
    /// </summary>
    /// <seealso cref="Options.SiteOptions.Description"/>
    public required string? Description { get; init; }

    /// <summary>
    ///     The publication date declared in the front matter, or <see langword="null"/> when it declares none.
    /// </summary>
    public required DateOnly? Date { get; init; }

    /// <summary>
    ///     Indicates whether the page loads a math renderer, as declared in its front matter. The value applies
    ///     to the page alone and is inherited by nothing.
    /// </summary>
    public required bool EnableMathRendering { get; init; }

    /// <summary>
    ///     Indicates whether the page loads a diagram renderer, as declared in its front matter. The value
    ///     applies to the page alone and is inherited by nothing.
    /// </summary>
    public required bool EnableDiagramRendering { get; init; }

    /// <summary>
    ///     The rendered HTML body, excluding the document shell.
    /// </summary>
    public required string Html { get; init; }

    /// <summary>
    ///     Indicates whether the site navigation links the page. The landing page, root-level pages
    ///     and section indexes are linked; pages inside a section are not.
    /// </summary>
    public required bool InNavigation { get; init; }

    /// <summary>
    ///     Indicates whether the feed publishes the page, as declared in its front matter. A page that declares
    ///     nothing stays out, so membership is never inherited from a directory above it.
    /// </summary>
    public required bool InFeed { get; init; }

    /// <summary>
    ///     The root-relative URLs of the stylesheets that apply to the page, outermost first, so that a
    ///     stylesheet deeper in the tree overrides one above it.
    /// </summary>
    public required IReadOnlyList<string> Stylesheets { get; init; }

    /// <summary>
    ///     The root-relative URLs of the scripts that apply to the page, outermost first, which is the order
    ///     they run in.
    /// </summary>
    public required IReadOnlyList<string> Scripts { get; init; }

    /// <summary>
    ///     The root-relative URL of the icon that applies to the page, which is the one declared nearest at or
    ///     above it, or <see langword="null"/> when no icon applies.
    /// </summary>
    public required string? Favicon { get; init; }
}
