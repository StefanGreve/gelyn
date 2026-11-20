using System.Net;
using System.Text;

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
    ///     Renders the table.
    /// </summary>
    /// <param name="context">
    ///     What the layout shares between the pages of the build, the catalog included.
    /// </param>
    /// <param name="page">
    ///     The page being rendered.
    /// </param>
    /// <returns>
    ///     The HTML of the navigation landmark holding the table, or an empty string when no page is listed.
    /// </returns>
    public static string Render(LayoutContext context, ContentPage page)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(page);

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

        if (rows.Count == 0) return string.Empty;

        var table = new StringBuilder();

        table.Append($"""<nav aria-label="{NavigationLabel}">""");
        table.Append("<table>");
        table.Append("<thead>");
        table.Append("<tr>");

        foreach ((string header, _) in Columns)
            table.Append($"<th>{header}</th>");

        table.Append("</tr>");
        table.Append("</thead>");
        table.Append("<tbody>");

        foreach ((ContentPage document, string type) in rows)
        {
            table.Append($"""<tr data-type="{type}">""");

            foreach ((_, Func<ContentPage, string> cell) in Columns)
                table.Append($"<td>{cell(document)}</td>");

            table.Append("</tr>");
        }

        table.Append("</tbody>");
        table.Append("</table>");
        table.Append("</nav>");

        return table.ToString();
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
