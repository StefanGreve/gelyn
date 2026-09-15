namespace Gelyn.Internals;

/// <summary>
///     The metadata vocabulary the generator recognizes.
/// </summary>
internal static class MetaDataConstants
{
    /// <summary>
    ///     The fence that opens and closes a metadata block.
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
