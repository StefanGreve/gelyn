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
///     <c>[TOD]</c> written inside a code span or a fenced block alone: Markdig escapes the brackets of both
///     before this component ever sees them.
/// </remarks>
internal static class TableOfDocumentsComponent
{
    /// <summary>
    ///     The rendered form of a <c>[TOD]</c> marker, which is a paragraph holding the literal and nothing else.
    /// </summary>
    internal const string Marker = "<p>[TOD]</p>";

    private const string FileType = "file";
    private const string FolderType = "folder";

    // The banner carries the other navigation landmark on every page, so this one is labelled to tell them apart.
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
    ///     The context the page is rendered against.
    /// </param>
    /// <returns>
    ///     The HTML of the navigation landmark holding the table, or an empty string when no page is listed.
    /// </returns>
    public static string Render(RenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string current = context.Page.OutputPath;
        int scopeLength = ResolveScopeLength(current);
        IReadOnlyList<ContentPage> listed = context.Documents.ListedIn(current.AsSpan(0, scopeLength));

        List<(ContentPage Page, string Type)> rows = new(listed.Count);

        // The listing comes ordered, and dropping the marker page from it leaves the rest in that order.
        foreach (ContentPage page in listed)
        {
            if (!string.Equals(page.OutputPath, current, StringComparison.Ordinal))
                rows.Add((page, ResolveType(page, scopeLength)));
        }

        if (rows.Count == 0) return string.Empty;

        var table = new StringBuilder();

        table.AppendLine($"""<nav aria-label="{NavigationLabel}">""");
        table.AppendLine("<table>");
        table.AppendLine("<thead>");
        table.AppendLine("<tr>");

        foreach ((string header, _) in Columns)
            table.AppendLine($"<th>{header}</th>");

        table.AppendLine("</tr>");
        table.AppendLine("</thead>");
        table.AppendLine("<tbody>");

        foreach ((ContentPage page, string type) in rows)
        {
            table.AppendLine($"""<tr data-type="{type}">""");

            foreach ((_, Func<ContentPage, string> cell) in Columns)
                table.AppendLine($"<td>{cell(page)}</td>");

            table.AppendLine("</tr>");
        }

        table.AppendLine("</tbody>");
        table.AppendLine("</table>");
        table.AppendLine("</nav>");

        return table.ToString();
    }

    #region Helpers

    // Counting the trailing separator in makes the prefix test a directory test and not a string one: "blog"
    // alone would match "blogs/post.html". A path carrying no separator yields zero, which every page passes.
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
