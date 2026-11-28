using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Gelyn.Core.Internals.Primitives;


namespace Gelyn.Tests.Core.Internals.Primitives;

/// <summary>
///     Unit tests for the <see cref="PageBuffer"/> class.
/// </summary>
[Category(Categories.Internal)]
public class PageBufferTests
{
    #region Append Tests

    /// <summary>
    ///     Verifies that bytes and text are written in the order they are appended, the text as UTF-8 with its
    ///     two-, three- and four-byte characters.
    /// </summary>
    [Test]
    public async Task Append_Test_BytesAndText_AreWrittenInOrder()
    {
        // Arrange
        using var buffer = new PageBuffer();

        // Act
        buffer.Append("<p>"u8);
        buffer.Append("Gr\U000000FC\U000000DFe, \U000020AC und \U0001F600");
        buffer.Append("</p>"u8);

        // Assert
        await Assert.That(Decode(buffer)).IsEqualTo("<p>Gr\U000000FC\U000000DFe, \U000020AC und \U0001F600</p>");
    }

    /// <summary>
    ///     Verifies that a text longer than the first array is kept whole across the growth of the buffer.
    /// </summary>
    [Test]
    public async Task Append_Test_TextLongerThanTheFirstArray_IsKeptWhole()
    {
        // Arrange
        string text = string.Concat(Enumerable.Repeat("0123456789", 10_000));
        using var buffer = new PageBuffer();

        // Act
        buffer.Append(text.AsSpan(0, 50_000));
        buffer.Append(text.AsSpan(50_000));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(buffer.Length).IsEqualTo(100_000);
            await Assert.That(Decode(buffer)).IsEqualTo(text);
        }
    }

    /// <summary>
    ///     Verifies that a text holding an unpaired surrogate is refused and that nothing of it is written, so
    ///     that a page with one fails before anything reaches the disk.
    /// </summary>
    [Test]
    public async Task Append_Test_UnpairedSurrogate_IsRefused()
    {
        // Arrange
        using var buffer = new PageBuffer();
        buffer.Append("a"u8);

        // Act and Assert
        await Assert.That(() => buffer.Append("b" + (char)0xD800)).Throws<EncoderFallbackException>();
        await Assert.That(Decode(buffer)).IsEqualTo("a");
    }

    #endregion // Append Tests

    #region FoldLineEndings Tests

    /// <summary>
    ///     Verifies that a carriage return is folded into a line feed whether it comes alone or paired, at
    ///     the start, in the middle or at the end of the text, and that a line feed is left as it is.
    /// </summary>
    [Test]
    [Arguments("a\r\nb", "a\nb")]
    [Arguments("a\rb", "a\nb")]
    [Arguments("a\r\nb\rc\nd", "a\nb\nc\nd")]
    [Arguments("\r\na", "\na")]
    [Arguments("a\r\n", "a\n")]
    [Arguments("a\r", "a\n")]
    [Arguments("\r", "\n")]
    [Arguments("\r\n\r\n", "\n\n")]
    [Arguments("\n\r", "\n\n")]
    public async Task FoldLineEndings_Test_CarriageReturn_BecomesALineFeed(string text, string expected)
    {
        // Arrange
        using var buffer = new PageBuffer();
        buffer.Append(text);

        // Act
        buffer.FoldLineEndings();

        // Assert
        await Assert.That(Decode(buffer)).IsEqualTo(expected);
    }

    /// <summary>
    ///     Verifies that a text holding no carriage return is left as it is.
    /// </summary>
    [Test]
    [Arguments("")]
    [Arguments("a")]
    [Arguments("a\nb\n")]
    public async Task FoldLineEndings_Test_TextWithoutACarriageReturn_IsLeftAsItIs(string text)
    {
        // Arrange
        using var buffer = new PageBuffer();
        buffer.Append(text);

        // Act
        buffer.FoldLineEndings();

        // Assert
        await Assert.That(Decode(buffer)).IsEqualTo(text);
    }

    /// <summary>
    ///     Verifies that the Unicode line breaks HTML treats as content are left alone, which is what sets
    ///     the fold apart from <see cref="string.ReplaceLineEndings(string)"/>.
    /// </summary>
    [Test]
    public async Task FoldLineEndings_Test_UnicodeLineBreaks_AreLeftAlone()
    {
        // Arrange
        using var buffer = new PageBuffer();
        buffer.Append("a\U00000085b\U00002028c\U00002029d\U0000000Ce\r\nf");

        // Act
        buffer.FoldLineEndings();

        // Assert
        await Assert.That(Decode(buffer)).IsEqualTo("a\U00000085b\U00002028c\U00002029d\U0000000Ce\nf");
    }

    #endregion // FoldLineEndings Tests

    #region Dispose Tests

    /// <summary>
    ///     Verifies that a disposed buffer holds nothing and refuses every use of its bytes, its array having gone
    ///     back to the pool.
    /// </summary>
    [Test]
    public async Task Dispose_Test_DisposedBuffer_RefusesEveryUse()
    {
        // Arrange
        var buffer = new PageBuffer();
        buffer.Append("a"u8);

        // Act
        buffer.Dispose();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(buffer.Length).IsEqualTo(0);
            await Assert.That(() => _ = buffer.WrittenSpan.Length).Throws<ObjectDisposedException>();
            await Assert.That(() => buffer.Append("b"u8)).Throws<ObjectDisposedException>();
            await Assert.That(() => buffer.Append("b")).Throws<ObjectDisposedException>();
            await Assert.That(() => buffer.FoldLineEndings()).Throws<ObjectDisposedException>();
            await Assert.That(() => buffer.Advance(0)).Throws<ObjectDisposedException>();
        }
    }

    /// <summary>
    ///     Verifies that disposing a buffer a second time does nothing, as the dispose pattern requires.
    /// </summary>
    [Test]
    public async Task Dispose_Test_SecondCall_DoesNothing()
    {
        // Arrange
        var buffer = new PageBuffer();
        buffer.Dispose();

        // Act and Assert
        await Assert.That(() => buffer.Dispose()).ThrowsNothing();
    }

    #endregion // Dispose Tests

    #region Helpers

    private static string Decode(PageBuffer buffer) =>
        Encoding.UTF8.GetString(buffer.WrittenSpan);

    #endregion
}
