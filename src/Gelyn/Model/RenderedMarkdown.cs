namespace Gelyn.Model;

/// <summary>
///     The result of rendering a content file: its front matter and the HTML body it produced.
/// </summary>
public sealed record RenderedMarkdown
{
    /// <summary>
    ///     Front matter parsed from the block at the top of the file.
    /// </summary>
    public FrontMatter? MetaData { get; init; }

    /// <summary>
    ///     The rendered HTML body, excluding the front matter block.
    /// </summary>
    public string? Html { get; init; }
}
