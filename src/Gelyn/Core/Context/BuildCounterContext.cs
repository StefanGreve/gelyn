namespace Gelyn.Core.Context;

/// <summary>
///     What one build counted and how long each of its phases took.
/// </summary>
/// <remarks>
///     The durations are raw timestamps rather than a <see cref="TimeSpan"/>, because the phases are timed
///     with <see cref="System.Diagnostics.Stopwatch.GetTimestamp"/> and converted once at the point they are
///     reported. Every member is <c>required</c> so that the call site names each value: the five durations
///     share a type, and a positional argument list lets two of them trade places without a compiler error.
/// </remarks>
internal sealed record BuildCounterContext
{
    /// <summary>
    ///     The number of files the scan located, pages and copies together.
    /// </summary>
    public required int ItemCount { get; init; }

    /// <summary>
    ///     The number of pages that were rendered.
    /// </summary>
    public required int PageCount { get; init; }

    /// <summary>
    ///     The number of files that reached the output directory, written and copied together.
    /// </summary>
    public required int FileCount { get; init; }

    /// <summary>
    ///     The number of files that were deleted from the output directory.
    /// </summary>
    public required int RemovedCount { get; init; }

    /// <summary>
    ///     The ticks the scan took.
    /// </summary>
    public required long ScanTicks { get; init; }

    /// <summary>
    ///     The ticks the render took.
    /// </summary>
    public required long RenderTicks { get; init; }

    /// <summary>
    ///     The ticks the compose took.
    /// </summary>
    public required long ComposeTicks { get; init; }

    /// <summary>
    ///     The ticks the writes took, the copy loop included.
    /// </summary>
    public required long WriteTicks { get; init; }

    /// <summary>
    ///     The ticks the reconciliation took.
    /// </summary>
    public required long RemoveTicks { get; init; }

    /// <summary>
    ///     How long the build took in total, measured before the reports are written.
    /// </summary>
    public required TimeSpan Elapsed { get; init; }
}
