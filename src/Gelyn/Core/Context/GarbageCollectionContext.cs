namespace Gelyn.Core.Context;

/// <summary>
///     What the garbage collector has counted so far, read at one point in time.
/// </summary>
/// <remarks>
///     The counters are process-wide and only ever grow, so two snapshots bracket a build and
///     <see cref="Since"/> yields what the build itself cost. A collection of a higher generation collects the
///     lower ones too, so the three counts nest: generation 0 is collected at least as often as generation 1,
///     and generation 1 at least as often as generation 2.
/// </remarks>
internal sealed record GarbageCollectionContext
{
    /// <summary>
    ///     The bytes allocated on the managed heap.
    /// </summary>
    public required long AllocatedBytes { get; init; }

    /// <summary>
    ///     The number of times generation 0 was collected.
    /// </summary>
    public required int Gen0Count { get; init; }

    /// <summary>
    ///     The number of times generation 1 was collected.
    /// </summary>
    public required int Gen1Count { get; init; }

    /// <summary>
    ///     The number of times generation 2 was collected.
    /// </summary>
    public required int Gen2Count { get; init; }

    /// <summary>
    ///     How long the collections paused the process in total.
    /// </summary>
    public required TimeSpan Pause { get; init; }

    /// <summary>
    ///     Reads the counters as they stand now.
    /// </summary>
    /// <returns>
    ///     The counters at the time of the call.
    /// </returns>
    public static GarbageCollectionContext Capture() =>
        new()
        {
            AllocatedBytes = GC.GetTotalAllocatedBytes(precise: true),
            Gen0Count = GC.CollectionCount(0),
            Gen1Count = GC.CollectionCount(1),
            Gen2Count = GC.CollectionCount(2),
            Pause = GC.GetTotalPauseDuration(),
        };

    /// <summary>
    ///     Computes what was allocated and collected between an earlier snapshot and this one.
    /// </summary>
    /// <param name="earlier">
    ///     The snapshot taken first.
    /// </param>
    /// <returns>
    ///     The difference of every counter.
    /// </returns>
    public GarbageCollectionContext Since(GarbageCollectionContext earlier)
    {
        ArgumentNullException.ThrowIfNull(earlier);

        return new()
        {
            AllocatedBytes = this.AllocatedBytes - earlier.AllocatedBytes,
            Gen0Count = this.Gen0Count - earlier.Gen0Count,
            Gen1Count = this.Gen1Count - earlier.Gen1Count,
            Gen2Count = this.Gen2Count - earlier.Gen2Count,
            Pause = this.Pause - earlier.Pause,
        };
    }
}
