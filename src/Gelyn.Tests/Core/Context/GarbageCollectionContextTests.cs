using System;
using System.Threading.Tasks;

using Gelyn.Core.Context;

namespace Gelyn.Tests.Core.Context;

/// <summary>
///     Unit tests for the <see cref="GarbageCollectionContext"/> record.
/// </summary>
public class GarbageCollectionContextTests
{
    /// <summary>
    ///     Verifies that the difference of two snapshots is taken counter by counter.
    /// </summary>
    [Test]
    public async Task Since_Test_TwoSnapshots_SubtractEveryCounter()
    {
        // Arrange
        var earlier = new GarbageCollectionContext
        {
            AllocatedBytes = 1_000,
            Gen0Count = 5,
            Gen1Count = 2,
            Gen2Count = 1,
            Pause = TimeSpan.FromMilliseconds(3),
        };

        var later = new GarbageCollectionContext
        {
            AllocatedBytes = 4_500,
            Gen0Count = 9,
            Gen1Count = 3,
            Gen2Count = 1,
            Pause = TimeSpan.FromMilliseconds(7.5),
        };

        // Act
        GarbageCollectionContext difference = later.Since(earlier);

        // Assert
        await Assert.That(difference).IsEqualTo(new GarbageCollectionContext
        {
            AllocatedBytes = 3_500,
            Gen0Count = 4,
            Gen1Count = 1,
            Gen2Count = 0,
            Pause = TimeSpan.FromMilliseconds(4.5),
        });
    }

    /// <summary>
    ///     Verifies that the counters only grow between two captures, which is what lets a difference stand
    ///     for the work in between.
    /// </summary>
    [Test]
    public async Task Capture_Test_TwoCaptures_NeverDecrease()
    {
        // Arrange
        var earlier = GarbageCollectionContext.Capture();

        // Act
        GC.KeepAlive(new byte[64 * 1024]);
        GarbageCollectionContext difference = GarbageCollectionContext.Capture().Since(earlier);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(difference.AllocatedBytes).IsGreaterThanOrEqualTo(64 * 1024);
            await Assert.That(difference.Gen0Count).IsGreaterThanOrEqualTo(0);
            await Assert.That(difference.Gen1Count).IsGreaterThanOrEqualTo(0);
            await Assert.That(difference.Gen2Count).IsGreaterThanOrEqualTo(0);
            await Assert.That(difference.Pause).IsGreaterThanOrEqualTo(TimeSpan.Zero);
        }
    }
}
