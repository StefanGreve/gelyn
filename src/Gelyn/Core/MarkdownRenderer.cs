using Gelyn.Model;

using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Markdig.Extensions.AutoLinks;
using Markdig.Extensions.EmphasisExtras;
using Markdig.Extensions.TableOfContents;
using Markdig.Extensions.Tables;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;

namespace Gelyn.Core;

/// <summary>
///     Converts Markdown source into HTML and extracts its front matter.
/// </summary>
/// <seealso href="https://github.com/xoofx/markdig"/>
/// <seealso href="https://xoofx.github.io/markdig/docs/extensions/"/>
internal sealed class MarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAlertBlocks()
        .UseAbbreviations()
        .UseAutoIdentifiers(AutoIdentifierOptions.GitHub)
        .UseTableOfContents(new TableOfContentsOptions { ListKind = TableOfContentsListKind.Ordered, MinLevel = 2 })
        .UseCitations()
        .UseDefinitionLists()
        .UseEmphasisExtras(EmphasisExtraOptions.Strikethrough)
        .UseFigures()
        .UseFootnotes()
        .UseMathematics()
        .UsePipeTables(new PipeTableOptions { UseGfmRules = true })
        .UseListExtras()
        .UseTaskLists()
        .UseDiagrams()
        .UseAutoLinks(new AutoLinkOptions { UseHttpsForWWWLinks = true })
        .UseRelativeFileLinks()
        .UseYamlFrontMatter()
        .Build();

    /// <summary>
    ///     Renders a markdown document.
    /// </summary>
    /// <param name="markdown">
    ///     The Markdown source, optionally preceded by a front matter block.
    /// </param>
    /// <returns>
    ///     The parsed front matter, the keys it declares that no member reads, and the rendered HTML body.
    /// </returns>
    public static RenderedMarkdown Render(string markdown)
    {
        MarkdownDocument document = Markdown.Parse(markdown, Pipeline);

        FrontMatter? frontMatter = null;
        IReadOnlyList<UnknownKey> unknownKeys = [];

        // UseYamlFrontMatter marks the block but leaves its contents unread, and the block parser opens
        // only at offset zero, so front matter can never sit anywhere but index 0.
        if (document.Count > 0 && document[0] is YamlFrontMatterBlock block)
        {
            ReadOnlySpan<char> source = markdown.AsSpan(block.Span.Start, block.Span.Length);

            if (!FrontMatterParser.TryParse(source, out frontMatter, out IReadOnlyList<UnknownKey>? unknown))
                unknownKeys = unknown;
        }

        return new RenderedMarkdown
        {
            FrontMatter = frontMatter,
            UnknownKeys = unknownKeys,
            Html = Markdown.ToHtml(document, Pipeline),
        };
    }
}
