namespace Gelyn.Model;

/// <summary>
///     The result of rendering a content file: its metadata and the HTML body it produced.
/// </summary>
internal sealed record RenderedMarkdown
{
    /// <summary>
    ///     Metadata parsed from the block at the top of the file.
    /// </summary>
    internal MetaData? MetaData { get; init; }

    /// <summary>
    ///     The rendered HTML body, excluding the metadata block.
    /// </summary>
    internal string? Html { get; init; }
}
