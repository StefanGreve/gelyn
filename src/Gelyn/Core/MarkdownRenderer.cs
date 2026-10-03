using System;

using Gelyn.Model;

using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;

namespace Gelyn.Core;

/// <summary>
///     Converts Markdown source into HTML and extracts its front matter.
/// </summary>
/// <seealso href="https://github.com/xoofx/markdig"/>
public sealed class MarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseYamlFrontMatter()
        .Build();

    /// <summary>
    ///     Renders a markdown document.
    /// </summary>
    /// <param name="markdown">
    ///     The Markdown source, optionally preceded by a front matter block.
    /// </param>
    /// <returns>
    ///     The parsed front matter and the rendered HTML body.
    /// </returns>
    public static RenderedMarkdown Render(string markdown)
    {
        MarkdownDocument document = Markdown.Parse(markdown, Pipeline);

        // UseYamlFrontMatter only marks the block, it does not parse it, and the parser rejects front
        // matter anywhere but the first block.
        FrontMatter frontMatter = document.Count > 0 && document[0] is YamlFrontMatterBlock block
            ? FrontMatterParser.Parse(markdown.AsSpan(block.Span.Start, block.Span.Length))
            : FrontMatter.Empty;

        return new RenderedMarkdown
        {
            FrontMatter = frontMatter,
            Html = Markdown.ToHtml(document, Pipeline),
        };
    }
}
