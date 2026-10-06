using System;

using Gelyn.Model;

using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Markdig.Extensions.AutoLinks;
using Markdig.Extensions.EmphasisExtras;
using Markdig.Extensions.Tables;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;

namespace Gelyn.Core;

/// <summary>
///     Converts Markdown source into HTML and extracts its front matter.
/// </summary>
/// <seealso href="https://github.com/xoofx/markdig"/>
/// <seealso href="https://xoofx.github.io/markdig/docs/extensions/"/>
public sealed class MarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAlertBlocks()
        .UseAbbreviations()
        .UseAutoIdentifiers(AutoIdentifierOptions.GitHub)
        .UseCitations()
        .UseDefinitionLists()
        .UseEmphasisExtras(EmphasisExtraOptions.Strikethrough)
        .UseFigures()
        .UseFooters()
        .UseFootnotes()
        .UseMathematics()
        .UsePipeTables(new PipeTableOptions { UseGfmRules = true })
        .UseListExtras()
        .UseTaskLists()
        .UseDiagrams()
        .UseAutoLinks(new AutoLinkOptions { UseHttpsForWWWLinks = true })
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

        // UseYamlFrontMatter marks the block but leaves its contents unread, and the block parser opens
        // only at offset zero, so front matter can never sit anywhere but index 0.
        FrontMatter? frontMatter = document.Count > 0 && document[0] is YamlFrontMatterBlock block
            ? FrontMatterParser.Parse(markdown.AsSpan(block.Span.Start, block.Span.Length))
            : null;

        return new RenderedMarkdown
        {
            FrontMatter = frontMatter,
            Html = Markdown.ToHtml(document, Pipeline),
        };
    }
}
