using System.Diagnostics.CodeAnalysis;
using System.Net;

using Gelyn.Components;
using Gelyn.Internals;

namespace Gelyn.Services;

/// <summary>
///     Wraps rendered page content in the shared document shell.
/// </summary>
[SuppressMessage("Performance", "CA1812", Justification = Justifications.ByDesign)]
internal sealed class PageLayout
{
    private readonly HeaderComponent _header;
    private readonly FooterComponent _footer;
    private readonly SiteOptions _options;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PageLayout"/> class.
    /// </summary>
    /// <param name="header">
    ///     The header fragment.
    /// </param>
    /// <param name="footer">
    ///     The footer fragment.
    /// </param>
    /// <param name="options">
    ///     Supplies the fallback title.
    /// </param>
    public PageLayout(HeaderComponent header, FooterComponent footer, SiteOptions options)
    {
        this._header = header;
        this._footer = footer;
        this._options = options;
    }

    /// <summary>
    ///     Renders a complete HTML document.
    /// </summary>
    /// <param name="title">
    ///     The page title, or <see langword="null"/> to fall back to the site title.
    /// </param>
    /// <param name="content">
    ///     The rendered page body.
    /// </param>
    /// <returns>
    ///     The complete HTML document.
    /// </returns>
    public string Render(string? title, string content)
    {
        // Titles come from front matter, which is untrusted input flowing straight into the document head.
        string encodedTitle = WebUtility.HtmlEncode(title ?? this._options.SiteTitle);

        string header = this._header.Render();
        string footer = this._footer.Render();

        return $"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>{encodedTitle}</title>
            </head>
            <body>
                {header}
                <main>
                    {content}
                </main>
                {footer}
            </body>
            </html>
        """;
    }
}
