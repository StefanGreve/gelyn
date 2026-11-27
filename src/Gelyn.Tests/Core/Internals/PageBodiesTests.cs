using System;
using System.Buffers;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Gelyn.Core.Internals;

namespace Gelyn.Tests.Core.Internals;

/// <summary>
///     Unit tests for the <see cref="PageBodies"/> class.
/// </summary>
[Category(Categories.Internal)]
public class PageBodiesTests
{
    private const int ThreadedCount = 1_000;

    #region Allocate Tests

    /// <summary>
    ///     Verifies that an allocated body has the requested length and keeps what is written to it.
    /// </summary>
    [Test]
    public async Task Allocate_Test_Body_KeepsWhatIsWritten()
    {
        // Arrange
        using var bodies = new PageBodies();

        // Act
        Memory<byte> body = bodies.Allocate(3);
        "abc"u8.CopyTo(body.Span);

        // Assert
        await Assert.That(DecodeBody(body)).IsEqualTo("abc");
    }

    /// <summary>
    ///     Verifies that a length of zero returns an empty block with no memory behind it.
    /// </summary>
    [Test]
    public async Task Allocate_Test_LengthZero_AllocatesNothing()
    {
        // Arrange
        using var bodies = new PageBodies();

        // Act
        Memory<byte> body = bodies.Allocate(0);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(body.IsEmpty).IsTrue();
            await Assert.That(MemoryMarshal.TryGetMemoryManager<byte, MemoryManager<byte>>(body, out _)).IsFalse();
        }
    }

    /// <summary>
    ///     Verifies that the bodies several threads allocate at once each keep their own contents.
    /// </summary>
    [Test]
    public async Task Allocate_Test_ManyThreadsAtOnce_KeepEveryBody()
    {
        // Arrange
        using var bodies = new PageBodies();

        // Act
        Memory<byte>[] allocated = AllocateBodiesInParallel(bodies);

        // Assert
        await Assert.That(allocated.Where(static (body, index) => body.Length == index + 1 && !body.Span.ContainsAnyExcept((byte)index)).Count()).IsEqualTo(ThreadedCount);
    }

    /// <summary>
    ///     Verifies that a disposed owner refuses to allocate, since nothing would free the body.
    /// </summary>
    [Test]
    public async Task Allocate_Test_DisposedOwner_Throws()
    {
        // Arrange
        using var bodies = new PageBodies();
        bodies.Dispose();

        // Act and Assert
        await Assert.That(() => bodies.Allocate(8)).Throws<ObjectDisposedException>();
    }

    #endregion // Allocate Tests

    #region Release Tests

    /// <summary>
    ///     Verifies that a released body refuses reads, since its memory is freed.
    /// </summary>
    [Test]
    public async Task Release_Test_ReleasedBody_RefusesReads()
    {
        // Arrange
        using var bodies = new PageBodies();
        Memory<byte> body = bodies.Allocate(8);

        // Act
        bodies.Release(body);

        // Assert
        await Assert.That(() => body.Span.Length).Throws<ObjectDisposedException>();
    }

    /// <summary>
    ///     Verifies that a second release of a body does nothing.
    /// </summary>
    [Test]
    public async Task Release_Test_SecondRelease_DoesNothing()
    {
        // Arrange
        using var bodies = new PageBodies();
        Memory<byte> body = bodies.Allocate(8);
        bodies.Release(body);

        // Act and Assert
        await Assert.That(() => bodies.Release(body)).ThrowsNothing();
    }

    /// <summary>
    ///     Verifies that a release after the owner is disposed does nothing.
    /// </summary>
    [Test]
    public async Task Release_Test_AfterDispose_DoesNothing()
    {
        // Arrange
        using var bodies = new PageBodies();
        Memory<byte> body = bodies.Allocate(8);
        bodies.Dispose();

        // Act and Assert
        await Assert.That(() => bodies.Release(body)).ThrowsNothing();
    }

    /// <summary>
    ///     Verifies that a managed array passed as a body is left alone.
    /// </summary>
    [Test]
    public async Task Release_Test_ManagedArray_StaysIntact()
    {
        // Arrange
        using var bodies = new PageBodies();
        byte[] array = "abc"u8.ToArray();

        // Act
        bodies.Release(array);

        // Assert
        await Assert.That(DecodeBody(array)).IsEqualTo("abc");
    }

    /// <summary>
    ///     Verifies that a body another owner allocated is left alone.
    /// </summary>
    [Test]
    public async Task Release_Test_BodyOfAnotherOwner_StaysReadable()
    {
        // Arrange
        using var bodies = new PageBodies();
        using var other = new PageBodies();
        Memory<byte> body = other.Allocate(3);
        "abc"u8.CopyTo(body.Span);

        // Act
        bodies.Release(body);

        // Assert
        await Assert.That(DecodeBody(body)).IsEqualTo("abc");
    }

    #endregion // Release Tests

    #region Dispose Tests

    /// <summary>
    ///     Verifies that disposing the owner frees a body that was never released.
    /// </summary>
    [Test]
    public async Task Dispose_Test_UnreleasedBody_RefusesReads()
    {
        // Arrange
        using var bodies = new PageBodies();
        Memory<byte> body = bodies.Allocate(8);

        // Act
        bodies.Dispose();

        // Assert
        await Assert.That(() => body.Span.Length).Throws<ObjectDisposedException>();
    }

    /// <summary>
    ///     Verifies that disposing the owner frees every body that several threads allocated at once, which takes
    ///     each of them having been registered.
    /// </summary>
    [Test]
    public async Task Dispose_Test_BodiesOfManyThreads_AreAllFreed()
    {
        // Arrange
        using var bodies = new PageBodies();
        Memory<byte>[] allocated = AllocateBodiesInParallel(bodies);

        // Act
        bodies.Dispose();

        // Assert
        await Assert.That(allocated.Count(IsFreed)).IsEqualTo(ThreadedCount);
    }

    #endregion // Dispose Tests

    #region Helpers

    private static Memory<byte>[] AllocateBodiesInParallel(PageBodies bodies) =>
        OrderedParallel.For(ThreadedCount, index => FillBody(bodies.Allocate(index + 1), (byte)index), CancellationToken.None);

    private static Memory<byte> FillBody(Memory<byte> body, byte value)
    {
        body.Span.Fill(value);
        return body;
    }

    private static string DecodeBody(ReadOnlyMemory<byte> body) => Encoding.UTF8.GetString(body.Span);

    private static bool IsFreed(Memory<byte> body)
    {
        try
        {
            _ = body.Span;
            return false;
        }
        catch (ObjectDisposedException)
        {
            return true;
        }
    }

    #endregion
}
