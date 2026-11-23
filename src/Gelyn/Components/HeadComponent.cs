using System.Net;

using Gelyn.Core;
using Gelyn.Core.Context;
using Gelyn.Core.Internals;
using Gelyn.Model;
using Gelyn.Model.Options;

namespace Gelyn.Components;

/// <summary>
///     The document metadata written to the head of every page.
/// </summary>
internal static class HeadComponent
{
    private const string LightTheme = "github-light";
    private const string DarkTheme = "github-dark";
    private const string LandingPage = "index.html";

    /// <summary>
    ///     Writes the metadata of a page, without the enclosing <c>head</c> element.
    /// </summary>
    /// <remarks>
    ///     The page's own description replaces the site's, and only a revised page carries a revision date.
    /// </remarks>
    /// <param name="context">
    ///     What the layout shares between the pages of the build.
    /// </param>
    /// <param name="page">
    ///     The page being rendered.
    /// </param>
    /// <param name="buffer">
    ///     The buffer the page is composed into.
    /// </param>
    public static void Render(LayoutContext context, ContentPage page, PageBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(buffer);

        SiteOptions options = context.Options;

        buffer.Append("""<meta charset="utf-8">"""u8);
        buffer.Append("""<meta name="viewport" content="width=device-width, initial-scale=1">"""u8);

        // Served at whichever address missed, so relative links must resolve against the root instead.
        if (page.IsNotFoundPage)
        {
            string root = $"{UrlUtils.ResolveBasePath(options.BaseUrl)}/";
            buffer.Append($"""<base href="{WebUtility.HtmlEncode(root)}">""");
        }

        buffer.Append($"""<meta name="generator" content="{Tool.Name} v{WebUtility.HtmlEncode(Tool.Version)}">""");

        if (!string.IsNullOrWhiteSpace(options.Author))
            buffer.Append($"""<meta name="author" content="{WebUtility.HtmlEncode(options.Author)}">""");

        string? description = page.Description ?? options.Description;

        if (!string.IsNullOrWhiteSpace(description))
            buffer.Append($"""<meta name="description" content="{WebUtility.HtmlEncode(description)}">""");

        if (page.Revised is DateOnly revised)
        {
            string date = revised.ToString(DateFormat.Machine, Tool.Culture);
            buffer.Append($"""<meta name="revised" content="{date}">""");
        }

        if (page.IsNotFoundPage)
            buffer.Append("""<meta name="robots" content="noindex">"""u8);

        if (options.BaseUrl is Uri baseUrl && !page.IsNotFoundPage)
        {
            string canonical = new Uri(baseUrl, page.Href).AbsoluteUri;
            buffer.Append($"""<link rel="canonical" href="{WebUtility.HtmlEncode(canonical)}">""");
        }

        buffer.Append(context.FeedLink.Span);

        string documentTitle = page.OutputPath.Equals(LandingPage, StringComparison.Ordinal) ? options.Title : $"{page.Title} - {options.Title}";
        buffer.Append($"<title>{WebUtility.HtmlEncode(documentTitle)}</title>");

        if (page.Favicon is string favicon)
            buffer.Append($"""<link rel="icon" href="{WebUtility.HtmlEncode(favicon)}">""");

        if (page.EnableMathRendering)
            buffer.AppendMathRenderer(options.KaTeXVersion);

        if (page.EnableDiagramRendering)
            buffer.AppendDiagramRenderer(options.MermaidVersion);

        if (page.EnableSyntaxHighlighting)
            buffer.AppendSyntaxHighlighter(options.ShikiVersion);

        // Outermost first, so the deepest stylesheet wins the cascade.
        foreach (string stylesheet in page.Stylesheets)
            buffer.Append($"""<link rel="stylesheet" href="{WebUtility.HtmlEncode(stylesheet)}">""");

        foreach (string script in page.Scripts)
            buffer.Append($"""<script defer src="{WebUtility.HtmlEncode(script)}"></script>""");
    }

    /// <summary>
    ///     Renders the link that announces the feed, once for the build.
    /// </summary>
    /// <param name="options">
    ///     The configuration snapshot the build is rendered against, which names the site and its base URL.
    /// </param>
    /// <param name="documents">
    ///     What the build knows about every page at once, which decides whether the feed is written.
    /// </param>
    /// <returns>
    ///     The HTML of the link, or an empty string unless the build writes the feed, which takes a base URL,
    ///     the feed switched on and a page that qualifies as an entry.
    /// </returns>
    public static string RenderFeedLink(SiteOptions options, DocumentCatalog documents)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(documents);

        if (options.BaseUrl is not Uri baseUrl || !options.EnableFeed || !documents.HasFeedEntries)
            return string.Empty;

        Uri root = UrlUtils.AsDirectory(baseUrl);

        string feed = WebUtility.HtmlEncode(new Uri(root, AtomFeed.FileName).AbsoluteUri);
        string title = WebUtility.HtmlEncode(options.Title);

        return $"""<link rel="alternate" type="{AtomFeed.ContentType}" href="{feed}" title="{title}">""";
    }

    #region Helpers

    private static void AppendMathRenderer(this PageBuffer head, string version)
    {
        string katex = $"https://cdn.jsdelivr.net/npm/katex@{WebUtility.HtmlEncode(version)}/dist";

        // The loading of KaTeX is deferred to speed up page rendering.
        // To automatically render math in text elements, include the auto-render extension.
        head.Append($$"""
            <link rel="stylesheet" href="{{katex}}/katex.min.css">
            <script defer src="{{katex}}/katex.min.js"></script>
            <script defer src="{{katex}}/contrib/auto-render.min.js" onload="renderMathInElement(document.body);"></script>
        """);
    }

    private static void AppendDiagramRenderer(this PageBuffer head, string version)
    {
        string mermaid = $"https://cdn.jsdelivr.net/npm/mermaid@{WebUtility.HtmlEncode(version)}/dist";

        // A module script defers by itself. The global lets a theme's script configure Mermaid before it draws
        // on the load event.
        head.Append($$"""
            <script type="module">
            import mermaid from "{{mermaid}}/mermaid.esm.min.mjs";
            window.mermaid = mermaid;
            mermaid.initialize({ startOnLoad: true });
            </script>
        """);
    }

    private static void AppendSyntaxHighlighter(this PageBuffer head, string version)
    {
        // The dist entry imports bare specifiers, so this is jsDelivr's bundled build.
        string shiki = $"https://cdn.jsdelivr.net/npm/shiki@{WebUtility.HtmlEncode(version)}/+esm";

        // The JavaScript engine needs no WebAssembly download. A language Shiki lacks is left as Markdig wrote
        // it. With no default color, the theme's stylesheet picks between the two palettes.
        head.Append($$"""
            <script type="module">
            import { bundledLanguages, createHighlighter, createJavaScriptRegexEngine } from "{{shiki}}";
            const blocks = new Map();
            for (const code of document.querySelectorAll("pre > code[class*='language-']")) {
                const lang = [...code.classList].find(name => name.startsWith("language-")).slice("language-".length);
                if (lang in bundledLanguages) blocks.set(code, lang);
            }
            if (blocks.size > 0) {
                const themes = { light: "{{LightTheme}}", dark: "{{DarkTheme}}" };
                const highlighter = await createHighlighter({ themes: Object.values(themes), langs: [...new Set(blocks.values())], engine: createJavaScriptRegexEngine() });
                for (const [code, lang] of blocks) {
                    const transformers = [{ code(node) { this.addClassToHast(node, code.className); } }];
                    code.parentElement.outerHTML = highlighter.codeToHtml(code.textContent.replace(/\n$/, ""), { lang, themes, defaultColor: false, transformers });
                }
            }
            </script>
        """);
    }

    #endregion
}
