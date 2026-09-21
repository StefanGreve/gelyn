using System.Diagnostics.CodeAnalysis;
using System.Net;

using Gelyn.Components;
using Gelyn.Internals;
using Gelyn.Model.Options;

using Microsoft.Extensions.Options;

namespace Gelyn.Services;

/// <summary>
///     Wraps rendered page content in the shared document shell.
/// </summary>
public sealed class PageLayout
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
    [SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = Justifications.ByDesign)]
    public PageLayout(HeaderComponent header, FooterComponent footer, IOptions<SiteOptions> options)
    {
        this._header = header;
        this._footer = footer;
        this._options = options.Value;
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
        string encodedTitle = WebUtility.HtmlEncode(title ?? this._options.Title);

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