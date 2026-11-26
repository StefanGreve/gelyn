using Gelyn.Core.Internals;
using Gelyn.Model;

using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Markdig.Extensions.AutoLinks;
using Markdig.Extensions.EmphasisExtras;
using Markdig.Extensions.RelativeFileLinks;
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
        .UseRelativeFileLinks(CreateRelativeFileLinkOptions())
        .UseYamlFrontMatter()
        .Build();

    /// <summary>
    ///     Renders a Markdown document.
    /// </summary>
    /// <param name="markdown">
    ///     The Markdown source, optionally preceded by a front matter block.
    /// </param>
    /// <returns>
    ///     The parsed front matter, the keys it declares that no member reads, the values it declares that
    ///     their members reject, and the rendered HTML body as UTF-8.
    /// </returns>
    /// <exception cref="System.Text.EncoderFallbackException">
    ///     The rendered HTML holds an unpaired surrogate.
    /// </exception>
    public static RenderedMarkdown Render(string markdown)
    {
        MarkdownDocument document = Markdown.Parse(markdown, Pipeline);

        FrontMatter? frontMatter = null;
        IReadOnlyList<UnknownKey> unknownKeys = [];
        IReadOnlyList<InvalidValue> invalidValues = [];

        // UseYamlFrontMatter marks the block but leaves its contents unread, and the block parser opens
        // only at offset zero, so front matter can never sit anywhere but index 0.
        if (document.Count > 0 && document[0] is YamlFrontMatterBlock block)
        {
            ReadOnlySpan<char> source = markdown.AsSpan(block.Span.Start, block.Span.Length);
            _ = FrontMatterParser.TryParse(source, out frontMatter, out unknownKeys, out invalidValues);
        }

        // Encoded once, at its exact size, from the pooled writer, so the HTML never becomes a string.
        using var writer = new PooledTextWriter();
        Markdown.ToHtml(document, writer, Pipeline);

        ReadOnlySpan<char> html = writer.WrittenSpan;
        byte[] body = new byte[Tool.Encoding.GetByteCount(html)];
        Tool.Encoding.GetBytes(html, body);

        return new RenderedMarkdown
        {
            FrontMatter = frontMatter,
            UnknownKeys = unknownKeys,
            InvalidValues = invalidValues,
            Body = body,
        };
    }

    #region Helpers

    // Only .md is rendered, so a link to any other file, a .markdown one included, has to keep reaching its copy.
    private static RelativeFileLinkOptions CreateRelativeFileLinkOptions()
    {
        var options = new RelativeFileLinkOptions();

        options.SourceExtensions.Clear();
        options.SourceExtensions.Add(FileExtensions.Markdown);
        options.TargetExtension = FileExtensions.Html;

        return options;
    }

    #endregion
}
