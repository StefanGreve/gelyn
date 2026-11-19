using System.Diagnostics;

using Gelyn.Core.Context;

namespace Gelyn.Diagnostics;

/// <summary>
///     Divides the duration of a build among the phases its counters report.
/// </summary>
/// <remarks>
///     Every mark ends the stretch since the previous one, so the phases and the skipped stretches add up to the
///     build total.
/// </remarks>
internal sealed class BuildClock
{
    private readonly Func<long> _timestamp;
    private readonly long[] _ticks = new long[(int)BuildPhase.Write + 1];
    private readonly long _started;
    private readonly GarbageCollectionContext? _heapStarted;
    private long _mark;
    private TimeSpan _elapsed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="BuildClock"/> class, which starts at once.
    /// </summary>
    /// <param name="timestamp">
    ///     Reads the current timestamp, in <see cref="Stopwatch.Frequency"/> ticks per second.
    /// </param>
    /// <param name="isMeasuring">
    ///     Indicates whether the build reports its counters.
    /// </param>
    internal BuildClock(Func<long> timestamp, bool isMeasuring)
    {
        ArgumentNullException.ThrowIfNull(timestamp);

        this._timestamp = timestamp;
        this._started = timestamp();
        this._mark = this._started;
        this._heapStarted = isMeasuring ? GarbageCollectionContext.Capture() : null;
        this.IsMeasuring = isMeasuring;
    }

    /// <summary>
    ///     Gets a value indicating whether the build reports its counters.
    /// </summary>
    /// <remarks>
    ///     The marks are taken either way, since a timestamp costs tens of nanoseconds. The heap snapshots and the
    ///     per-page timestamps cost more and are only taken when this is set.
    /// </remarks>
    public bool IsMeasuring { get; }

    /// <summary>
    ///     Starts a clock that reads <see cref="Stopwatch.GetTimestamp"/>.
    /// </summary>
    /// <param name="isMeasuring">
    ///     Indicates whether the build reports its counters.
    /// </param>
    /// <returns>
    ///     The running clock.
    /// </returns>
    public static BuildClock Start(bool isMeasuring) =>
        new(Stopwatch.GetTimestamp, isMeasuring);

    /// <summary>
    ///     Charges the stretch since the previous mark to a phase, and marks the current timestamp.
    /// </summary>
    /// <param name="phase">
    ///     The phase the stretch belongs to.
    /// </param>
    public void Charge(BuildPhase phase)
    {
        long now = this._timestamp();
        this._ticks[(int)phase] += now - this._mark;
        this._mark = now;
    }

    /// <summary>
    ///     Marks the current timestamp without charging the stretch since the previous mark to any phase.
    /// </summary>
    /// <remarks>
    ///     The build total still includes the skipped stretch.
    /// </remarks>
    public void Skip() =>
        this._mark = this._timestamp();

    /// <summary>
    ///     Divides the stretch since the previous mark between composing and writing, in the ratio of the thread
    ///     time each took, and marks the current timestamp.
    /// </summary>
    /// <remarks>
    ///     Pages are composed and written on several threads at once, so neither has a wall time of its own.
    /// </remarks>
    /// <param name="composeTicks">
    ///     The thread time spent composing, summed over every thread.
    /// </param>
    /// <param name="writeTicks">
    ///     The thread time spent writing, summed over every thread.
    /// </param>
    public void Split(long composeTicks, long writeTicks)
    {
        long now = this._timestamp();
        long wall = now - this._mark;
        long whole = composeTicks + writeTicks;
        long compose = whole == 0 ? 0 : (long)((double)wall * composeTicks / whole);

        this._ticks[(int)BuildPhase.Compose] += compose;
        this._ticks[(int)BuildPhase.Write] += wall - compose;
        this._mark = now;
    }

    /// <summary>
    ///     Takes the build total.
    /// </summary>
    /// <returns>
    ///     The time since the clock started.
    /// </returns>
    public TimeSpan Stop()
    {
        this._elapsed = Stopwatch.GetElapsedTime(this._started, this._timestamp());

        return this._elapsed;
    }

    /// <summary>
    ///     Gathers what the build counted together with what the clock measured.
    /// </summary>
    /// <remarks>
    ///     Carries the total that <see cref="Stop"/> took, and the heap figures since the clock started.
    /// </remarks>
    /// <param name="itemCount">
    ///     The number of source files the scan located.
    /// </param>
    /// <param name="pageCount">
    ///     The number of pages that were rendered.
    /// </param>
    /// <param name="fileCount">
    ///     The number of output files that reached the output directory.
    /// </param>
    /// <param name="removedCount">
    ///     The number of files that were deleted from the output directory.
    /// </param>
    /// <returns>
    ///     The counters of the build.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    ///     The clock does not measure, so it took no heap snapshot when it started.
    /// </exception>
    public BuildCounterContext CollectCounters(int itemCount, int pageCount, int fileCount, int removedCount)
    {
        if (this._heapStarted is not GarbageCollectionContext heapStarted)
            throw new InvalidOperationException("A clock that does not measure takes no heap snapshot to count from.");

        return new BuildCounterContext
        {
            ItemCount = itemCount,
            PageCount = pageCount,
            FileCount = fileCount,
            RemovedCount = removedCount,
            ScanTicks = this._ticks[(int)BuildPhase.Scan],
            RenderTicks = this._ticks[(int)BuildPhase.Render],
            ComposeTicks = this._ticks[(int)BuildPhase.Compose],
            WriteTicks = this._ticks[(int)BuildPhase.Write],
            RemoveTicks = this._ticks[(int)BuildPhase.Remove],
            Elapsed = this._elapsed,
            GarbageCollection = GarbageCollectionContext.Capture().Since(heapStarted),
        };
    }
}
