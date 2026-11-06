using Gelyn.Model;

namespace Gelyn.Core;

/// <summary>
///     What a build knows about all of its pages at once, computed once and read by every page it renders.
/// </summary>
/// <remarks>
///     A component asked about the whole site would otherwise scan every page for every page it renders,
///     which grows with the square of the site. The catalog answers each such question from a lookup built
///     in one pass over the pages.
/// </remarks>
internal sealed class DocumentCatalog
{
    private const string IndexFileName = "index.html";

    private readonly Dictionary<string, ContentPage[]> _listings;
    private readonly Dictionary<string, ContentPage[]>.AlternateLookup<ReadOnlySpan<char>> _lookup;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DocumentCatalog"/> class.
    /// </summary>
    /// <param name="pages">
    ///     Every page the site is built from, in scan order, which is the order that breaks ties in a listing.
    /// </param>
    public DocumentCatalog(IReadOnlyList<ContentPage> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);

        this.HasFeedEntries = AtomFeed.HasEntries(pages);
        this._listings = CollectListings(pages);
        this._lookup = this._listings.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    /// <summary>
    ///     Indicates whether any page qualifies as a feed entry, which is what decides whether the feed is
    ///     written and so whether a page may announce or link it.
    /// </summary>
    public bool HasFeedEntries { get; }

    /// <summary>
    ///     The pages a table of documents in a directory lists: the dated pages directly in the directory and
    ///     the dated index of each directory directly below it, newest first and by title among a date.
    /// </summary>
    /// <param name="scope">
    ///     The output-relative path of the directory including its trailing separator, which is empty at the
    ///     output root.
    /// </param>
    /// <returns>
    ///     The listed pages, among which the caller may find the page it renders, or an empty list when the
    ///     directory lists nothing.
    /// </returns>
    public IReadOnlyList<ContentPage> ListedIn(ReadOnlySpan<char> scope) =>
        this._lookup.TryGetValue(scope, out ContentPage[]? pages) ? pages : [];

    #region Helpers

    private static Dictionary<string, ContentPage[]> CollectListings(IReadOnlyList<ContentPage> pages)
    {
        var collected = new Dictionary<string, List<ContentPage>>(StringComparer.Ordinal);

        foreach (ContentPage page in pages)
        {
            // A page declares a date once it is ready to be found, so one declaring none is a draft, and the
            // not-found page has no address of its own to list.
            if (page.Date is null || page.IsNotFoundPage) continue;

            ReadOnlySpan<char> outputPath = page.OutputPath;
            int scopeLength = outputPath.LastIndexOf(Path.DirectorySeparatorChar) + 1;

            Add(collected, outputPath[..scopeLength], page);

            // An index stands for its directory in the listing one level up, where the directory is a row.
            if (scopeLength > 0 && outputPath[scopeLength..].Equals(IndexFileName, StringComparison.Ordinal))
            {
                int parentLength = outputPath[..(scopeLength - 1)].LastIndexOf(Path.DirectorySeparatorChar) + 1;
                Add(collected, outputPath[..parentLength], page);
            }
        }

        // A stable sort over scan order, so that pages sharing a date and a title keep the order they were found in.
        return collected.ToDictionary(
            static listing => listing.Key,
            static listing => listing.Value
                .OrderByDescending(static page => page.Date)
                .ThenBy(static page => page.Title, StringComparer.Ordinal)
                .ToArray(),
            StringComparer.Ordinal);
    }

    private static void Add(Dictionary<string, List<ContentPage>> collected, ReadOnlySpan<char> scope, ContentPage page)
    {
        var lookup = collected.GetAlternateLookup<ReadOnlySpan<char>>();

        if (!lookup.TryGetValue(scope, out List<ContentPage>? listed))
        {
            listed = [];
            collected[scope.ToString()] = listed;
        }

        listed.Add(page);
    }

    #endregion
}
