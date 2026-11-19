using System.Collections.ObjectModel;

namespace Gelyn.Core.Context;

/// <summary>
///     What one level of the output walk carries while the build prunes the output directory.
/// </summary>
/// <remarks>
///     <see cref="Removed"/> is an accumulator rather than an input: every level appends to the one list, so
///     the caller reads the whole walk's deletions off the instance it passed in. <see cref="Published"/> and
///     <see cref="PublishedDirectories"/> are read-only in practice, and only <see cref="Prefix"/> changes from one
///     level to the next.
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
    ///     The output-relative path of every directory a published file lands in, keyed without regard to case and
    ///     mapped to the spelling it is published under.
    /// </summary>
    public required Dictionary<string, string> PublishedDirectories { get; init; }

    /// <summary>
    ///     Receives the output-relative path of each file that was deleted.
    /// </summary>
    public required Collection<string> Removed { get; init; }
}
