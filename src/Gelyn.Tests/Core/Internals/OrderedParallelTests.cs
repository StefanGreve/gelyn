using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Gelyn.Core.Internals;

namespace Gelyn.Tests.Core.Internals;

/// <summary>
///     Unit tests for the <see cref="OrderedParallel"/> class.
/// </summary>
public class OrderedParallelTests
{
    #region Select Tests

    /// <summary>
    ///     Verifies that every result lands at the index of the item it was projected from.
    /// </summary>
    [Test]
    public async Task Select_Test_Results_KeepTheOrderOfTheItems()
    {
        // Arrange
        int[] items = [.. Enumerable.Range(0, 1000)];

        // Act
        string[] results = OrderedParallel.Select(items, static item => $"#{item}", CancellationToken.None);

        // Assert
        // Joined instead of compared as a sequence, because IsEquivalentTo disregards the order under test.
        await Assert.That(string.Join(',', results))
            .IsEqualTo(string.Join(',', items.Select(static item => $"#{item}")));
    }

    /// <summary>
    ///     Verifies that every item is projected exactly once.
    /// </summary>
    [Test]
    public async Task Select_Test_EveryItem_IsProjectedOnce()
    {
        // Arrange
        int[] items = [.. Enumerable.Range(0, 1000)];
        int[] calls = new int[items.Length];

        // Act
        OrderedParallel.Select(items, item => Interlocked.Increment(ref calls[item]), CancellationToken.None);

        // Assert
        await Assert.That(calls.All(static count => count == 1)).IsTrue();
    }

    /// <summary>
    ///     Verifies that the failure of the first failing item surfaces with its own type, unwrapped, even when
    ///     items after it fail sooner. The first failing item is held back, so that the later ones fail while it
    ///     is still running, as a naive parallel loop would report them.
    /// </summary>
    [Test]
    [Repeat(10)]
    public async Task Select_Test_SeveralFailures_SurfaceTheFirstInOrder()
    {
        // Arrange
        int[] items = [.. Enumerable.Range(0, 256)];

        static string Project(int item)
        {
            if (item == 17)
            {
                Thread.Sleep(20);
                throw new FileNotFoundException("first");
            }

            if (item is 18 or 130 or 255)
                throw new IOException("later");

            return string.Empty;
        }

        // Act and Assert
        await Assert.That(() => OrderedParallel.Select(items, Project, CancellationToken.None))
            .ThrowsExactly<FileNotFoundException>()
            .WithMessage("first");
    }

    /// <summary>
    ///     Verifies that a failure still lets every item below it run, which is what makes the surfacing
    ///     failure independent of scheduling.
    /// </summary>
    [Test]
    public async Task Select_Test_Failure_LetsEveryEarlierItemRun()
    {
        // Arrange
        const int failing = 40;
        int[] items = [.. Enumerable.Range(0, 64)];
        int[] calls = new int[items.Length];

        int Project(int item)
        {
            if (item == failing)
                throw new IOException("failed");

            return Interlocked.Increment(ref calls[item]);
        }

        // Act
        await Assert.That(() => OrderedParallel.Select(items, Project, CancellationToken.None))
            .ThrowsExactly<IOException>();

        // Assert
        await Assert.That(calls.Take(failing).All(static count => count == 1)).IsTrue();
    }

    /// <summary>
    ///     Verifies that a canceled token surfaces as a cancellation, not as an aggregate of failures.
    /// </summary>
    [Test]
    public async Task Select_Test_CanceledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        int[] items = [.. Enumerable.Range(0, 100)];
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Act and Assert
        await Assert.That(() => OrderedParallel.Select(items, static item => item, cancellation.Token))
            .Throws<OperationCanceledException>();
    }

    #endregion // Select Tests
}
