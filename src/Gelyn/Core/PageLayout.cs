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
    ///     The head is rendered here and never passed in, because its contents depend on the page: its own
    ///     description replaces the site's, and only a revised page carries a revision date. The comment thread is
    ///     rendered here for the same reason. The body is expanded on the way through, every <c>[TOD]</c>
    ///     marker it carries giving way to a table of documents.
    /// </remarks>
    /// <param name="context">
    ///     The context the page is rendered against.
    /// </param>
    /// <param name="banner">
    ///     The banner, rendered once for the build.
    /// </param>
    /// <param name="footer">
    ///     The footer, rendered once for the build.
    /// </param>
    /// <returns>
    ///     The complete HTML document.
    /// </returns>
    public static string Render(RenderContext context, Banner banner, Footer footer)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(banner);
        ArgumentNullException.ThrowIfNull(footer);

        string language = WebUtility.HtmlEncode(context.Options.Language);
        string head = HeadComponent.Render(context);
        string comments = CommentsComponent.Render(context);

        string body = context.Page.Html.Contains(TableOfDocumentsComponent.Marker, StringComparison.Ordinal)
            ? context.Page.Html.Replace(TableOfDocumentsComponent.Marker, TableOfDocumentsComponent.Render(context), StringComparison.Ordinal)
            : context.Page.Html;

        // Each page is assembled from slices of the shared banner and footer through the holes below, which keeps
        // every page from making a private copy of either.
        Slot headerSlot = banner.CutFor(context.Page);
        Slot footerSlot = footer.CutFor(context.Page);

        return $"""
            <!DOCTYPE html>
            <html lang="{language}">
            <head>
                {head}
            </head>
            <body>
                {headerSlot.Before}{headerSlot.Fragment}{headerSlot.After}
                <main>
                    {body}
                </main>
                {comments}
                <hr />
                {footerSlot.Before}{footerSlot.Fragment}{footerSlot.After}
            </body>
            </html>
        """;
    }
}
