namespace Gelyn.Model;

/// <summary>
///     The result of rendering a content file: its front matter and the HTML body it produced.
/// </summary>
public sealed record RenderedMarkdown
{
    /// <summary>
    ///     Front matter parsed from the block at the top of the file, or <see cref="FrontMatter.Empty"/> when
    ///     the file declares none.
    /// </summary>
    public required FrontMatter FrontMatter { get; init; }

    /// <summary>
    ///     The rendered HTML body, excluding the front matter block.
    /// </summary>
    public required string Html { get; init; }
}
