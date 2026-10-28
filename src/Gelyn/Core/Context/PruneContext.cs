namespace Gelyn.Core.Context;

/// <summary>
///     What one level of the output walk carries while the build reconciles the output directory.
/// </summary>
/// <remarks>
///     <see cref="Removed"/> is an accumulator rather than an input: every level appends to the one list, so
///     the caller reads the whole walk's deletions off the instance it passed in. <see cref="Published"/> is
///     read-only in practice, and only <see cref="Prefix"/> changes from one level to the next.
/// </remarks>
internal sealed record PruneContext
{
    /// <summary>
    ///     The output-relative segments leading to the directory being walked, empty at the output root.
    /// </summary>
    public required IReadOnlyList<string> Prefix { get; init; }

    /// <summary>
    ///     The output-relative path of every file the site consists of.
    /// </summary>
    public required HashSet<string> Published { get; init; }

    /// <summary>
    ///     Receives the output-relative path of each file that was deleted.
    /// </summary>
    public required List<string> Removed { get; init; }
}
