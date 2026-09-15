using Gelyn.Model;

namespace Gelyn.Abstractions;

/// <summary>
///     Converts Markdown source into HTML and extracts its metadata.
/// </summary>
internal abstract class MarkdownRendererContract
{
    /// <summary>
    ///     Renders a Markdown document.
    /// </summary>
    /// <param name="markdown">
    ///     The Markdown source, optionally preceded by a metadata block.
    /// </param>
    /// <returns>
    ///     The parsed metadata and the rendered HTML body.
    /// </returns>
    public abstract RenderedMarkdown Render(string markdown);
}
