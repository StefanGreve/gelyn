using System;
using System.Net;

using Gelyn.Components;
using Gelyn.Model;

namespace Gelyn.Services;

/// <summary>
///     Wraps rendered page content in the shared document shell.
/// </summary>
public static class PageLayout
{
    /// <summary>
    ///     Renders a complete HTML document.
    /// </summary>
    /// <param name="context">
    ///     Supplies the page to render and the pages the navigation links to.
    /// </param>
    /// <returns>
    ///     The complete HTML document.
    /// </returns>
    public static string Render(RenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string language = WebUtility.HtmlEncode(context.Options.Language);
        string head = HeadComponent.Render(context);
        string header = HeaderComponent.Render(context);
        string footer = FooterComponent.Render(context);

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
