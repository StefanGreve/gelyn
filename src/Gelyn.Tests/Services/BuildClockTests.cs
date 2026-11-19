using System;
using System.Diagnostics;
using System.Threading.Tasks;

using Gelyn.Core.Context;
using Gelyn.Services;

namespace Gelyn.Tests.Services;

/// <summary>
///     Unit tests for the <see cref="BuildClock"/> class.
/// </summary>
/// <remarks>
///     Every timestamp is a whole number of seconds, which converts to an exact <see cref="TimeSpan"/> at every
///     <see cref="Stopwatch.Frequency"/> the supported platforms report.
/// </remarks>
[Category(Categories.Internal)]
public class BuildClockTests
{
    private static readonly long Second = Stopwatch.Frequency;

    #region Charge Tests

    /// <summary>
    ///     Verifies that every phase receives the stretch since the previous mark, and a phase charged twice the
    ///     sum of both stretches, so that no two phases can trade values unnoticed.
    /// </summary>
    [Test]
    public async Task Charge_Test_EachPhase_ReceivesTheStretchSinceThePreviousMark()
    {
        // Arrange
        long now = 0;
        var clock = new BuildClock(() => now, isMeasuring: true);

        // Act
        now = 1 * Second;
        clock.Charge(BuildPhase.Scan);
        now = 3 * Second;
        clock.Charge(BuildPhase.Render);
        now = 6 * Second;
        clock.Charge(BuildPhase.Write);
        now = 10 * Second;
        clock.Charge(BuildPhase.Remove);
        now = 15 * Second;
        clock.Charge(BuildPhase.Compose);
        now = 21 * Second;
        clock.Charge(BuildPhase.Write);
        clock.Stop();

        BuildCounterContext counters = clock.CollectCounters(0, 0, 0, 0);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(counters.ScanTicks).IsEqualTo(1 * Second);
            await Assert.That(counters.RenderTicks).IsEqualTo(2 * Second);
            await Assert.That(counters.RemoveTicks).IsEqualTo(4 * Second);
            await Assert.That(counters.ComposeTicks).IsEqualTo(5 * Second);
            await Assert.That(counters.WriteTicks).IsEqualTo(9 * Second);
        }
    }

    #endregion

    #region Skip Tests

    /// <summary>
    ///     Verifies that a skipped stretch is charged to no phase but still counts toward the build total.
    /// </summary>
    [Test]
    public async Task Skip_Test_SkippedStretch_CountsTowardTheTotalOnly()
    {
        // Arrange
        long now = 0;
        var clock = new BuildClock(() => now, isMeasuring: true);

        // Act
        now = 4 * Second;
        clock.Charge(BuildPhase.Render);
        now = 9 * Second;
        clock.Skip();
        now = 16 * Second;
        clock.Charge(BuildPhase.Write);
        now = 20 * Second;
        TimeSpan elapsed = clock.Stop();

        BuildCounterContext counters = clock.CollectCounters(0, 0, 0, 0);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(counters.RenderTicks).IsEqualTo(4 * Second);
            await Assert.That(counters.WriteTicks).IsEqualTo(7 * Second);
            await Assert.That(counters.ScanTicks + counters.RemoveTicks + counters.ComposeTicks).IsEqualTo(0);
            await Assert.That(elapsed).IsEqualTo(TimeSpan.FromSeconds(20));
        }
    }

    #endregion

    #region Split Tests

    /// <summary>
    ///     Verifies that the stretch of the page pass is divided between composing and writing in the ratio of
    ///     the thread time each took.
    /// </summary>
    [Test]
    public async Task Split_Test_Stretch_IsDividedInTheRatioOfThreadTime()
    {
        // Arrange
        long now = 0;
        var clock = new BuildClock(() => now, isMeasuring: true);

        // Act
        now = 8 * Second;
        clock.Split(composeTicks: 1 * Second, writeTicks: 3 * Second);
        clock.Stop();

        BuildCounterContext counters = clock.CollectCounters(0, 0, 0, 0);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(counters.ComposeTicks).IsEqualTo(2 * Second);
            await Assert.That(counters.WriteTicks).IsEqualTo(6 * Second);
        }
    }

    /// <summary>
    ///     Verifies that a page pass without measured thread time charges its whole stretch to writing, so that
    ///     the phases still add up to the total.
    /// </summary>
    [Test]
    public async Task Split_Test_NoThreadTime_ChargesTheStretchToWrite()
    {
        // Arrange
        long now = 0;
        var clock = new BuildClock(() => now, isMeasuring: true);

        // Act
        now = 5 * Second;
        clock.Split(composeTicks: 0, writeTicks: 0);
        clock.Stop();

        BuildCounterContext counters = clock.CollectCounters(0, 0, 0, 0);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(counters.ComposeTicks).IsEqualTo(0);
            await Assert.That(counters.WriteTicks).IsEqualTo(5 * Second);
        }
    }

    #endregion

    #region Counter Tests

    /// <summary>
    ///     Verifies that the counters carry the counts they are given and the total that the clock stopped at.
    /// </summary>
    [Test]
    public async Task CollectCounters_Test_CountsAndTotal_AreCarriedOver()
    {
        // Arrange
        long now = 0;
        var clock = new BuildClock(() => now, isMeasuring: true);

        // Act
        now = 17 * Second;
        TimeSpan elapsed = clock.Stop();
        now = 30 * Second;

        BuildCounterContext counters = clock.CollectCounters(11, 7, 13, 3);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(counters.ItemCount).IsEqualTo(11);
            await Assert.That(counters.PageCount).IsEqualTo(7);
            await Assert.That(counters.FileCount).IsEqualTo(13);
            await Assert.That(counters.RemovedCount).IsEqualTo(3);
            await Assert.That(counters.Elapsed).IsEqualTo(elapsed);
            await Assert.That(counters.Elapsed).IsEqualTo(TimeSpan.FromSeconds(17));
        }
    }

    /// <summary>
    ///     Verifies that a clock that does not measure refuses to collect counters, because it took no heap
    ///     snapshot to count from.
    /// </summary>
    [Test]
    public async Task CollectCounters_Test_ClockThatDoesNotMeasure_Throws()
    {
        // Arrange
        var clock = new BuildClock(static () => 0, isMeasuring: false);

        // Act
        clock.Stop();

        // Assert
        await Assert.That(() => clock.CollectCounters(0, 0, 0, 0)).Throws<InvalidOperationException>();
    }

    #endregion
}
