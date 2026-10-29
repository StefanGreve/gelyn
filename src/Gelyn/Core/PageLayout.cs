using System.Net;

using Gelyn.Components;
using Gelyn.Core.Context;

namespace Gelyn.Core;

/// <summary>
///     Wraps rendered page content in the shared document shell.
/// </summary>
internal static class PageLayout
{
    /// <summary>
    ///     Renders a complete HTML document.
    /// </summary>
    /// <remarks>
    ///     The head is rendered here and never passed in, because its contents depend on the page: a page
    ///     that declares no description or date carries none of the corresponding tags. The body is expanded
    ///     on the way through, every <c>[TOD]</c> marker it carries giving way to a table of documents.
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

        // Expand [TOD] marker if present
        string body = context.Page.Html.Contains(TableOfDocumentsComponent.Marker, StringComparison.Ordinal)
            ? context.Page.Html.Replace(TableOfDocumentsComponent.Marker, TableOfDocumentsComponent.Render(context), StringComparison.Ordinal)
            : context.Page.Html;

        return $"""
            <!DOCTYPE html>
            <html lang="{language}">
            <head>
                {head}
            </head>
            <body>
                {header}
                <main>
                    {body}
                </main>
                <hr />
                {footer}
            </body>
            </html>
        """;
    }
}
