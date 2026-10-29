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

    private const string IndexFileName = "index.html";
    private const string FileType = "file";
    private const string FolderType = "folder";

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
    ///     The HTML of the table, or an empty string when no page is listed.
    /// </returns>
    public static string Render(RenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string scope = ResolveScope(context.Page.OutputPath);

        List<ContentPage> documents = [.. context.Documents
            .Where(page => IsListed(page, context.Page.OutputPath, scope))
            .OrderByDescending(static page => page.Date)
            .ThenBy(static page => page.Title, StringComparer.Ordinal)];

        if (documents.Count == 0) return string.Empty;

        var table = new StringBuilder();

        table.AppendLine("<table>");
        table.AppendLine("<thead>");
        table.AppendLine("<tr>");

        foreach ((string header, _) in Columns)
            table.AppendLine($"<th>{header}</th>");

        table.AppendLine("</tr>");
        table.AppendLine("</thead>");
        table.AppendLine("<tbody>");

        foreach (ContentPage page in documents)
        {
            table.AppendLine($"""<tr data-type="{ResolveType(page, scope)}">""");

            foreach ((_, Func<ContentPage, string> cell) in Columns)
                table.AppendLine($"<td>{cell(page)}</td>");

            table.AppendLine("</tr>");
        }

        table.AppendLine("</tbody>");
        table.AppendLine("</table>");

        return table.ToString();
    }

    #region Helpers

    // Keeping the trailing separator makes StartsWith a directory test and not a string one: "blog" alone
    // would match "blogs/post.html". The content root has none, yielding the empty prefix every page starts with.
    private static string ResolveScope(string outputPath)
    {
        int separator = outputPath.LastIndexOf(Path.DirectorySeparatorChar);

        return separator < 0
            ? string.Empty
            : outputPath[..(separator + 1)];
    }

    // No table lists a draft, its own page, or anything the scope does not hold directly. StartsWith comes
    // first because ResolveType slices from the scope's length and means nothing outside it.
    private static bool IsListed(ContentPage page, string current, string scope) =>
        !IsDraft(page)
        && !string.Equals(page.OutputPath, current, StringComparison.Ordinal)
        && page.OutputPath.StartsWith(scope, StringComparison.Ordinal)
        && ResolveType(page, scope) is not null;

    // A document declares a date once it is ready to be found, so the absence of one is what marks it unfinished.
    private static bool IsDraft(ContentPage page) =>
        page.Date is null;

    // A directory listing: pages beside the marker, plus each directory below it as its own index. Null is
    // anything deeper. The segment matches in full, because myindex.md ends the same way.
    private static string? ResolveType(ContentPage page, string scope)
    {
        ReadOnlySpan<char> relativePath = page.OutputPath.AsSpan(scope.Length);
        int separator = relativePath.IndexOf(Path.DirectorySeparatorChar);

        if (separator < 0) return FileType;

        return relativePath[(separator + 1)..].Equals(IndexFileName, StringComparison.Ordinal)
            ? FolderType
            : null;
    }

    private static string RenderDate(ContentPage page)
    {
        if (page.Date is not DateOnly date) return string.Empty;

        string machine = date.ToString(DateFormat.Machine, Tool.Culture);
        return $"""<time datetime="{machine}">{date.ToString(DateFormat.Display, Tool.Culture)}</time>""";
    }

    private static string RenderTitle(ContentPage page) =>
        $"""<a href="{WebUtility.HtmlEncode(page.Href)}">{WebUtility.HtmlEncode(page.Title)}</a>""";

    #endregion
}
