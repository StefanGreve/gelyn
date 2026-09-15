namespace Gelyn.Internals;

/// <summary>
///     The front matter vocabulary the generator recognizes.
/// </summary>
internal static class FrontMatterConstants
{
    /// <summary>
    ///     The fence that opens and closes a front matter block.
    /// </summary>
    internal const string Delimiter = "---";

    /// <summary>
    ///     The key supplying the page title.
    /// </summary>
    internal const string Title = "title";

    /// <summary>
    ///     The key supplying the publication date.
    /// </summary>
    internal const string Date = "date";
}
