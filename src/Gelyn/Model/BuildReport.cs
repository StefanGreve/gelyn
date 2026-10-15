using System;
using System.Collections.Generic;

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
}
