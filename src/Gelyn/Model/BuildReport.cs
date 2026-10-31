namespace Gelyn.Model;

/// <summary>
///     What one run of the generator produced.
/// </summary>
internal sealed record BuildReport
{
    /// <summary>
    ///     The output-relative paths that were rendered and written, landing page first.
    /// </summary>
    public required IReadOnlyList<string> Pages { get; init; }

    /// <summary>
    ///     The output-relative paths that were written from no source of their own, which today is the
    ///     syndication feed and nothing else.
    /// </summary>
    public required IReadOnlyList<string> Generated { get; init; }

    /// <summary>
    ///     The output-relative paths that were copied unchanged.
    /// </summary>
    public required IReadOnlyList<string> Copies { get; init; }

    /// <summary>
    ///     The output-relative paths that were deleted, because no source accounts for them any more. Holds
    ///     files alone; a directory left empty by those deletions is removed without being listed.
    /// </summary>
    public required IReadOnlyList<string> Removed { get; init; }

    /// <summary>
    ///     How long the build took, covering discovery, layout, writing and copying, but not the startup that
    ///     precedes it, which for a small site is the larger share of the time the caller waits.
    /// </summary>
    public required TimeSpan Elapsed { get; init; }

    /// <summary>
    ///     The number of files the build wrote, copied and deleted, which is the total every command reports.
    /// </summary>
    public int FileCount =>
        this.Pages.Count + this.Generated.Count + this.Copies.Count + this.Removed.Count;
}
