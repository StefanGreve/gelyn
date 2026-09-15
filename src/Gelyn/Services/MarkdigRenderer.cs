using System;

using Gelyn.Abstractions;
using Gelyn.Model;

using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;

namespace Gelyn.Services;

/// <summary>
///     Renders Markdown with <see href="https://github.com/xoofx/markdig">Markdig</see>.
/// </summary>
public sealed class MarkdigRenderer : MarkdownRendererContract
{
    private readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
        .UseYamlFrontMatter()
        .Build();

    /// <inheritdoc/>
    public override RenderedMarkdown Render(string markdown)
    {
        MarkdownDocument document = Markdown.Parse(markdown, this._pipeline);

        // UseYamlFrontMatter only marks the block, it does not parse it, and the parser rejects front
        // matter anywhere but the first block.
        FrontMatter metaData = document.Count > 0 && document[0] is YamlFrontMatterBlock block
            ? FrontMatterParser.Parse(markdown.AsSpan(block.Span.Start, block.Span.Length))
            : FrontMatter.Empty;

        return new RenderedMarkdown
        {
            MetaData = metaData,
            Html = Markdown.ToHtml(document, this._pipeline),
        };
    }
}
