using System.Diagnostics;

namespace Gelyn.Extensions;

/// <summary>
///     Durations read off the high-resolution timestamps <see cref="Stopwatch"/> hands out.
/// </summary>
internal static class StopwatchExtensions
{
    /// <summary>
    ///     Defines extensions for a timestamp an interval is measured from.
    /// </summary>
    /// <param name="startingTimestamp">
    ///     A timestamp obtained from <see cref="Stopwatch.GetTimestamp"/>, used as the receiver.
    /// </param>
    extension(long startingTimestamp)
    {
        /// <summary>
        ///     The interval from the timestamp to now, in milliseconds. Every read takes a new timestamp, and
        ///     so reports a longer interval than the read before it.
        /// </summary>
        public double ElapsedMilliseconds =>
            Stopwatch.GetElapsedTime(startingTimestamp).TotalMilliseconds;
    }

    /// <summary>
    ///     Defines extensions for an interval that has already been measured.
    /// </summary>
    /// <param name="elapsedTicks">
    ///     The difference between two timestamps obtained from <see cref="Stopwatch.GetTimestamp"/>, used as
    ///     the receiver. One such tick is a <see cref="Stopwatch.Frequency"/>th of a second, not the 100
    ///     nanoseconds of a <see cref="System.TimeSpan"/>.
    /// </param>
    extension(long elapsedTicks)
    {
        /// <summary>
        ///     The interval the ticks span, in milliseconds.
        /// </summary>
        public double TotalMilliseconds =>
            Stopwatch.GetElapsedTime(0, elapsedTicks).TotalMilliseconds;
    }
}
