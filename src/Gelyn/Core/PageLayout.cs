using System;
using System.Net;

using Gelyn.Components;
using Gelyn.Model;

namespace Gelyn.Core;

/// <summary>
///     Wraps rendered page content in the shared document shell.
/// </summary>
public static class PageLayout
{
    /// <summary>
    ///     Renders a complete HTML document.
    /// </summary>
    /// <remarks>
    ///     The head is rendered here rather than passed in, because its contents depend on the page: a page
    ///     that declares no description, date or math carries none of the corresponding tags.
    /// </remarks>
    /// <param name="context">
    ///     The context the page is rendered against.
    /// </param>
    /// <param name="header">
    ///     The rendered banner.
    /// </param>
    /// <param name="footer">
    ///     The rendered footer.
    /// </param>
    /// <returns>
    ///     The complete HTML document.
    /// </returns>
    public static string Render(RenderContext context, string header, string footer)
    {
        ArgumentNullException.ThrowIfNull(context);

        string language = WebUtility.HtmlEncode(context.Options.Language);
        string head = HeadComponent.Render(context);

        return $"""
            <!DOCTYPE html>
            <html lang="{language}">
            <head>
                {head}
            </head>
            <body>
                {header}
                <main>
                    {context.Page.Html}
                </main>
                <hr />
                {footer}
            </body>
            </html>
        """;
    }
}
