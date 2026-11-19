using System.Collections.ObjectModel;

namespace Gelyn.Core.Context;

/// <summary>
///     What the output walk carries while the build prunes the output directory.
/// </summary>
/// <remarks>
///     <see cref="Removed"/> and <see cref="Missing"/> are accumulators rather than inputs: every level appends to
///     the one list and strikes directories off the one set, so the caller reads the whole walk's deletions and the
///     directories it did not find off the instance it passed in.
/// </remarks>
internal sealed record PruneContext
{
    /// <summary>
    ///     The output-relative path of every file the site consists of.
    /// </summary>
    public required HashSet<string> Published { get; init; }

    /// <summary>
    ///     The output-relative path of every directory a published file lands in, the root as the empty string,
    ///     keyed without regard to case so that a directory kept under another spelling finds the one it is
    ///     published under.
    /// </summary>
    public required HashSet<string> PublishedDirectories { get; init; }

    /// <summary>
    ///     The published directories the walk has not found under their published spelling: every one of them
    ///     before the walk, and those the build has to create after it.
    /// </summary>
    public required HashSet<string> Missing { get; init; }

    /// <summary>
    ///     Receives the output-relative path of each file that was deleted.
    /// </summary>
    public required Collection<string> Removed { get; init; }
}
