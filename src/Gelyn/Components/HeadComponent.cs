using System;
using System.Globalization;
using System.Net;
using System.Text;

using Gelyn.Core.Internals;
using Gelyn.Model;
using Gelyn.Model.Options;

namespace Gelyn.Components;

/// <summary>
///     The document metadata written to the head of every page.
/// </summary>
internal static class HeadComponent
{
    private const string DateFormat = "yyyy-MM-dd";
    private const string MathRenderer = "renderMathInElement(document.body);";
    private const string DiagramRenderer = "mermaid.initialize({ startOnLoad: true });";

    /// <summary>
    ///     Renders the metadata.
    /// </summary>
    /// <param name="context">
    ///     The context the page is rendered against.
    /// </param>
    /// <returns>
    ///     The HTML of the fragment, without the enclosing <c>head</c> element.
    /// </returns>
    public static string Render(RenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        StringBuilder head = new();
        SiteOptions options = context.Options;

        head.AppendLine("""<meta charset="utf-8">""");
        head.AppendLine("""<meta name="viewport" content="width=device-width, initial-scale=1">""");
        head.AppendLine($"""<meta name="generator" content="{Tool.Name} v{WebUtility.HtmlEncode(Tool.Version)}">""");

        if (!string.IsNullOrWhiteSpace(options.Author))
            head.AppendLine($"""<meta name="author" content="{WebUtility.HtmlEncode(options.Author)}">""");

        string? description = context.Page.Description ?? options.Description;

        if (!string.IsNullOrWhiteSpace(description))
            head.AppendLine($"""<meta name="description" content="{WebUtility.HtmlEncode(description)}">""");

        if (context.Page.Date is DateOnly revised)
        {
            string date = revised.ToString(DateFormat, CultureInfo.InvariantCulture);
            head.AppendLine($"""<meta name="revised" content="{date}">""");
        }

        if (options.BaseUrl is Uri baseUrl)
        {
            string canonical = new Uri(baseUrl, context.Page.Href).AbsoluteUri;
            head.AppendLine($"""<link rel="canonical" href="{WebUtility.HtmlEncode(canonical)}">""");
        }

        head.AppendLine($"<title>{WebUtility.HtmlEncode(context.Page.Title)}</title>");

        if (context.Page.EnableMathRendering)
        {
            string katex = $"https://cdn.jsdelivr.net/npm/katex@{WebUtility.HtmlEncode(options.KaTeXVersion)}/dist";

            head.AppendLine($"""<link rel="stylesheet" href="{katex}/katex.min.css">""");
            // The loading of KaTeX is deferred to speed up page rendering
            head.AppendLine($"""<script defer src="{katex}/katex.min.js"></script>""");
            // To automatically render math in text elements, include the auto-render extension
            head.AppendLine($"""<script defer src="{katex}/contrib/auto-render.min.js" onload="{MathRenderer}"></script>""");
        }

        if (context.Page.EnableDiagramRendering)
        {
            string mermaid = $"https://cdn.jsdelivr.net/npm/mermaid@{WebUtility.HtmlEncode(options.MermaidVersion)}/dist";

            // The module entry is smaller then a bundle and fetches only the diagram types the page uses.
            // A module script defers by itself, so it needs no defer attribute.
            head.AppendLine("""<script type="module">""");
            head.AppendLine($"""import mermaid from "{mermaid}/mermaid.esm.min.mjs";""");
            head.AppendLine(DiagramRenderer);
            head.AppendLine("</script>");
        }

        // Outermost first, so the deepest stylesheet wins the cascade.
        foreach (string stylesheet in context.Page.Stylesheets)
            head.AppendLine($"""<link rel="stylesheet" href="{WebUtility.HtmlEncode(stylesheet)}">""");

        foreach (string script in context.Page.Scripts)
            head.AppendLine($"""<script defer src="{WebUtility.HtmlEncode(script)}"></script>""");

        return head.ToString();
    }
}
