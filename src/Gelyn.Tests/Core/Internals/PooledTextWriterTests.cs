using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Gelyn.Core.Internals;

namespace Gelyn.Tests.Core.Internals;

/// <summary>
///     Unit tests for the <see cref="PooledTextWriter"/> class.
/// </summary>
[Category(Categories.Internal)]
public class PooledTextWriterTests
{
    #region Write Tests

    /// <summary>
    ///     Verifies that every overridden write appends its characters in the order of the calls, and that a null
    ///     string appends nothing.
    /// </summary>
    [Test]
    public async Task Write_Test_EachOverride_AppendsInOrder()
    {
        // Arrange
        using var writer = new PooledTextWriter();

        // Act
        writer.Write('a');
        writer.Write("bc");
        writer.Write((string?)null);
        writer.Write(['x', 'd', 'e', 'y'], 1, 2);
        writer.Write("fg".AsSpan());

        // Assert
        await Assert.That(writer.WrittenSpan.ToString()).IsEqualTo("abcdefg");
    }

    /// <summary>
    ///     Verifies that a text longer than the first array is kept whole across the growth of the buffer.
    /// </summary>
    [Test]
    public async Task Write_Test_TextLongerThanTheFirstArray_IsKeptWhole()
    {
        // Arrange
        string text = string.Concat(Enumerable.Repeat("0123456789", 10_000));
        using var writer = new PooledTextWriter();

        // Act
        writer.Write(text.AsSpan(0, 50_000));
        writer.Write(text.AsSpan(50_000));

        // Assert
        await Assert.That(writer.WrittenSpan.ToString()).IsEqualTo(text);
    }

    /// <summary>
    ///     Verifies that a member the writer does not override reaches the overrides through the base class.
    /// </summary>
    [Test]
    public async Task Write_Test_MemberNotOverridden_ReachesTheOverrides()
    {
        // Arrange
        using var writer = new PooledTextWriter();

        // Act
        writer.Write(42);
        writer.Write(" and ");
        writer.Write(true);

        // Assert
        await Assert.That(writer.WrittenSpan.ToString()).IsEqualTo("42 and True");
    }

    #endregion // Write Tests

    #region WriteLine Tests

    /// <summary>
    ///     Verifies that every overridden line ends with the new line that Markdig sets on its writer.
    /// </summary>
    [Test]
    public async Task WriteLine_Test_NewLineAsMarkdigSetsIt_EndsEachLine()
    {
        // Arrange
        using var writer = new PooledTextWriter { NewLine = "\n" };

        // Act
        writer.WriteLine();
        writer.WriteLine("a");
        writer.WriteLine("b".AsSpan());

        // Assert
        await Assert.That(writer.WrittenSpan.ToString()).IsEqualTo("\na\nb\n");
    }

    #endregion // WriteLine Tests

    #region Encoding Tests

    /// <summary>
    ///     Verifies that the writer reports UTF-16, the encoding of the characters it holds.
    /// </summary>
    [Test]
    public async Task Encoding_Test_Writer_ReportsUtf16()
    {
        // Arrange
        using var writer = new PooledTextWriter();

        // Act and Assert
        await Assert.That(writer.Encoding).IsEqualTo(Encoding.Unicode);
    }

    #endregion // Encoding Tests

    #region Dispose Tests

    /// <summary>
    ///     Verifies that a disposed writer refuses reads and writes, since its array went back to the pool.
    /// </summary>
    [Test]
    public async Task Dispose_Test_DisposedWriter_RefusesReadsAndWrites()
    {
        // Arrange
        using var writer = new PooledTextWriter();
        writer.Write("a");

        // Act
        writer.Dispose();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(() => writer.WrittenSpan.ToString()).Throws<ObjectDisposedException>();
            await Assert.That(() => writer.Write('b')).Throws<ObjectDisposedException>();
        }
    }

    #endregion // Dispose Tests
}
