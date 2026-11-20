using System.Net;

using Gelyn.Core.Context;
using Gelyn.Core.Internals;
using Gelyn.Model;

namespace Gelyn.Components;

/// <summary>
///     The table of documents rendered in place of a <c>[TOD]</c> marker.
/// </summary>
/// <remarks>
///     The marker is matched in the rendered HTML and not in the Markdown source, which is what leaves a
///     <c>[TOD]</c> written inside a code span or a fenced block alone: Markdig wraps both in markup that never
///     equals the marker.
/// </remarks>
internal static class TableOfDocumentsComponent
{
    /// <summary>
    ///     The rendered form of a <c>[TOD]</c> marker, which is a paragraph holding the literal and nothing else.
    /// </summary>
    internal const string Marker = "<p>[TOD]</p>";

    private const string FileType = "file";
    private const string FolderType = "folder";

    // The banner carries the other navigation landmark on every page, so this one is labeled to tell them apart.
    private const string NavigationLabel = "documents";

    // One description of the columns, so that a column added later is an entry here and not a second edit.
    // Each cell writes its own markup and escapes its own text, because neither column is plain text.
    private static readonly (string Header, Func<ContentPage, string> Cell)[] Columns =
    [
        ("Date", RenderDate),
        ("Title", RenderTitle),
    ];

    /// <summary>
    ///     Writes the navigation landmark holding the table, or nothing when no page is listed.
    /// </summary>
    /// <param name="context">
    ///     What the layout shares between the pages of the build, the catalog included.
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

        string current = page.OutputPath;
        int scopeLength = ResolveScopeLength(current);
        IReadOnlyList<ContentPage> listed = context.Documents.ListedIn(current.AsSpan(0, scopeLength));

        List<(ContentPage Page, string Type)> rows = new(listed.Count);

        // The listing comes ordered, and dropping the marker page from it leaves the rest in that order.
        foreach (ContentPage document in listed)
        {
            if (!string.Equals(document.OutputPath, current, StringComparison.Ordinal))
                rows.Add((document, ResolveType(document, scopeLength)));
        }

        if (rows.Count == 0) return;

        buffer.Append($"""<nav aria-label="{NavigationLabel}">""");
        buffer.Append("<table><thead><tr>"u8);

        foreach ((string header, _) in Columns)
            buffer.Append($"<th>{header}</th>");

        buffer.Append("</tr></thead><tbody>"u8);

        foreach ((ContentPage document, string type) in rows)
        {
            buffer.Append($"""<tr data-type="{type}">""");

            foreach ((_, Func<ContentPage, string> cell) in Columns)
                buffer.Append($"<td>{cell(document)}</td>");

            buffer.Append("</tr>"u8);
        }

        buffer.Append("</tbody></table></nav>"u8);
    }

    #region Helpers

    // The scope keeps its trailing separator, as the catalog keys do, and is empty for a page at the output root.
    private static int ResolveScopeLength(string outputPath) =>
        outputPath.LastIndexOf(Path.DirectorySeparatorChar) + 1;

    // The catalog lists a page directly in the scope or an index one level below it and nothing else, so a
    // separator past the scope is all that tells a directory from a file.
    private static string ResolveType(ContentPage page, int scopeLength) =>
        page.OutputPath.AsSpan(scopeLength).Contains(Path.DirectorySeparatorChar)
            ? FolderType
            : FileType;

    // The catalog lists no dateless page, so the throw states an invariant and is never reached.
    private static string RenderDate(ContentPage page)
    {
        DateOnly date = page.Date
            ?? throw new InvalidOperationException("A listed document always declares a date.");

        string machine = date.ToString(DateFormat.Machine, Tool.Culture);
        return $"""<time datetime="{machine}">{date.ToString(DateFormat.Listing, Tool.Culture)}</time>""";
    }

    private static string RenderTitle(ContentPage page) =>
        $"""<a href="{WebUtility.HtmlEncode(page.Href)}">{WebUtility.HtmlEncode(page.Title)}</a>""";

    #endregion
}
