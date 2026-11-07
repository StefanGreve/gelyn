namespace Gelyn.Model;

/// <summary>
///     A key a front matter block declares that no <see cref="FrontMatter"/> member reads, such as a
///     misspelled <c>titel</c>.
/// </summary>
internal sealed record UnknownKey
{
    /// <summary>
    ///     The key as written in the block.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///     The one-based line of the source file the key is declared on.
    /// </summary>
    public required int Line { get; init; }
}
