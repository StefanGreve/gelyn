using System;
using System.Collections.Generic;

namespace Gelyn.Model;

/// <summary>
///     What one run of the generator produced.
/// </summary>
public sealed record BuildReport
{
    /// <summary>
    ///     The output-relative paths that were written, landing page first.
    /// </summary>
    public required IReadOnlyList<string> Files { get; init; }

    /// <summary>
    ///     How long the build took, covering discovery, layout and writing, but not the startup that precedes
    ///     it, which for a small site is the larger share of the time the caller waits.
    /// </summary>
    public required TimeSpan Elapsed { get; init; }
}
