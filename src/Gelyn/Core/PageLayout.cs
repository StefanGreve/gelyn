using Gelyn.Components;
using Gelyn.Core.Context;
using Gelyn.Core.Internals;
using Gelyn.Model;

namespace Gelyn.Core;

/// <summary>
///     Wraps rendered page content in the shared document shell.
/// </summary>
internal static class PageLayout
{
    /// <summary>
    ///     Composes a complete HTML document into a buffer, as UTF-8 with every line ending on a line feed.
    /// </summary>
    /// <remarks>
    ///     The head is written here and never passed in, because its contents depend on the page: its own
    ///     description replaces the site's, and only a revised page carries a revision date. The comment thread,
    ///     rendered once for the build, is written for a page that asks for one. The body is written in segments
    ///     around every <c>[TOD]</c> marker it carries, each giving way to a table of documents.
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
    /// <exception cref="System.Text.EncoderFallbackException">
    ///     The page carries an unpaired surrogate.
    /// </exception>
    public static void Render(LayoutContext context, ContentPage page, PageBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(buffer);

        buffer.Append("<!DOCTYPE html>\n<html lang=\""u8);
        buffer.Append(context.Language.Span);
        buffer.Append("\">\n<head>"u8);
        HeadComponent.Render(context, page, buffer);
        buffer.Append("</head>\n<body>"u8);

        // Each page is assembled from slices of the shared banner and footer, which keeps every page from making
        // a private copy of either.
        context.Banner.CutFor(page).WriteTo(buffer);
        buffer.Append("<main>"u8);
        buffer.AppendBody(context, page);
        buffer.Append("</main>"u8);
        CommentsComponent.Render(context, page, buffer);
        buffer.Append("<hr />"u8);
        context.Footer.CutFor(page).WriteTo(buffer);
        buffer.Append("</body>\n</html>\n"u8);

        buffer.FoldLineEndings();
    }

    #region Helpers

    private static void AppendBody(this PageBuffer buffer, LayoutContext context, ContentPage page)
    {
        ReadOnlySpan<byte> body = page.Body.Span;

        while (true)
        {
            int marker = body.IndexOf(TableOfDocumentsComponent.Marker);

            if (marker < 0) break;

            buffer.Append(body[..marker]);
            TableOfDocumentsComponent.Render(context, page, buffer);
            body = body[(marker + TableOfDocumentsComponent.Marker.Length)..];
        }

        buffer.Append(body);
    }

    #endregion
}
