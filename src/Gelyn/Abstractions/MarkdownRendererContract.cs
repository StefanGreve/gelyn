using Gelyn.Model;

namespace Gelyn.Abstractions;

/// <summary>
///     Converts Markdown source into HTML and extracts its front matter.
/// </summary>
public abstract class MarkdownRendererContract
{
    /// <summary>
    ///     Renders a Markdown document.
    /// </summary>
    /// <param name="markdown">
    ///     The Markdown source, optionally preceded by a front matter block.
    /// </param>
    /// <returns>
    ///     The parsed front matter and the rendered HTML body.
    /// </returns>
    public abstract RenderedMarkdown Render(string markdown);
}
