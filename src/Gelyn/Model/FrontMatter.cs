namespace Gelyn.Model;

/// <summary>
///     Front matter declared in the block at the top of a content file.
/// </summary>
/// <remarks>
///     A property is <see langword="null"/> when the block declares no value for it, and a date also when its
///     value is not written as <c>yyyy-MM-dd</c>. A value declared as an empty string is kept as one, so the
///     two cases stay distinguishable.
/// </remarks>
internal sealed record FrontMatter
{
    /// <summary>
    ///     The page title, or <see langword="null"/> when the block omits it.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    ///     The publication date, or <see langword="null"/> when the block omits it or does not write it as
    ///     <c>yyyy-MM-dd</c>.
    /// </summary>
    public DateOnly? Date { get; set; }

    /// <summary>
    ///     The date the page was last revised, or <see langword="null"/> when the block omits it or does not write
    ///     it as <c>yyyy-MM-dd</c>. A page that has not been revised since it was published omits it.
    /// </summary>
    public DateOnly? Revised { get; set; }

    /// <summary>
    ///     The page description, or <see langword="null"/> when the block omits it.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     Indicates whether the page loads a math renderer, or <see langword="null"/> when the block omits it.
    /// </summary>
    public bool? EnableMathRendering { get; set; }

    /// <summary>
    ///     Indicates whether the page loads a diagram renderer, or <see langword="null"/> when the block omits
    ///     it.
    /// </summary>
    public bool? EnableDiagramRendering { get; set; }

    /// <summary>
    ///     Indicates whether the page loads a syntax highlighter, or <see langword="null"/> when the block
    ///     omits it.
    /// </summary>
    public bool? EnableSyntaxHighlighting { get; set; }

    /// <summary>
    ///     Indicates whether the page renders a comment thread, or <see langword="null"/> when the block omits it.
    /// </summary>
    public bool? EnableComments { get; set; }

    /// <summary>
    ///     Indicates whether the feed publishes the page, or <see langword="null"/> when the block omits it.
    /// </summary>
    public bool? EnableFeed { get; set; }

    /// <summary>
    ///     The stylesheets the page links in addition to the ones placement gives it, as paths relative to the
    ///     page's own file, or <see langword="null"/> when the block omits them.
    /// </summary>
    public IReadOnlyList<string?>? Stylesheets { get; set; }

    /// <summary>
    ///     The scripts the page loads in addition to the ones placement gives it, as paths relative to the
    ///     page's own file, or <see langword="null"/> when the block omits them.
    /// </summary>
    public IReadOnlyList<string?>? Scripts { get; set; }
}
