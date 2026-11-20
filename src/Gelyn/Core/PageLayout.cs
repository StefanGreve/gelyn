using Gelyn.Components;
using Gelyn.Core.Context;
using Gelyn.Model;

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
    ///     description replaces the site's, and only a revised page carries a revision date. The comment thread,
    ///     rendered once for the build, is written for a page that asks for one. The body is expanded on the way
    ///     through, every <c>[TOD]</c> marker it carries giving way to a table of documents.
    /// </remarks>
    /// <param name="context">
    ///     What the layout shares between the pages of the build.
    /// </param>
    /// <param name="page">
    ///     The page being rendered.
    /// </param>
    /// <returns>
    ///     The complete HTML document.
    /// </returns>
    public static string Render(LayoutContext context, ContentPage page)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(page);

        string head = HeadComponent.Render(context, page);
        string comments = CommentsComponent.Render(context, page);

        string body = page.Html.Contains(TableOfDocumentsComponent.Marker, StringComparison.Ordinal)
            ? page.Html.Replace(TableOfDocumentsComponent.Marker, TableOfDocumentsComponent.Render(context, page), StringComparison.Ordinal)
            : page.Html;

        // Each page is assembled from slices of the shared banner and footer through the holes below, which keeps
        // every page from making a private copy of either.
        Slot headerSlot = context.Banner.CutFor(page);
        Slot footerSlot = context.Footer.CutFor(page);

        return $"""
            <!DOCTYPE html>
            <html lang="{context.Language}">
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
