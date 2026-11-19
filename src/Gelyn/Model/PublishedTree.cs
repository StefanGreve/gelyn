namespace Gelyn.Model;

/// <summary>
///     Every path a build publishes below the output directory: the files, and the directories on the way to them.
/// </summary>
internal sealed record PublishedTree
{
    /// <summary>
    ///     The output-relative path of every file the site consists of.
    /// </summary>
    public required HashSet<string> Files { get; init; }

    /// <summary>
    ///     The output-relative path of every directory a file lands in, the root as the empty string first, each
    ///     directory ahead of those below it, in the order the files first reach them.
    /// </summary>
    public required IReadOnlyList<string> Directories { get; init; }
}
