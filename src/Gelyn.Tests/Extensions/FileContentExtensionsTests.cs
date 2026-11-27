using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using Gelyn.Extensions;
using Gelyn.Tests.Fixtures;

using Testably.Abstractions.Testing;

namespace Gelyn.Tests.Extensions;

/// <summary>
///     Unit tests for the <see cref="FileContentExtensions"/> class.
/// </summary>
[Category(Categories.Internal)]
public class FileContentExtensionsTests
{
    // Characters of one to four bytes in UTF-8, the last a surrogate pair in UTF-16.
    private const string Text = "Gr\U000000FC\U000000DFe, \U000020AC und \U0001F600";

    private static readonly string Source = Path.GetFullPath("/site/content/logo.png");
    private static readonly string Destination = Path.GetFullPath("/site/_site/logo.png");
    private static readonly string Page = Path.GetFullPath("/site/content/index.md");

    #region CopyContent Tests

    /// <summary>
    ///     Verifies that the copy holds the bytes of the source and the modification time given, not the one the
    ///     source carries at the time of the copy.
    /// </summary>
    [Test]
    public async Task CopyContent_Test_Copy_CarriesTheBytesAndTheGivenTime()
    {
        // Arrange
        byte[] expected = [0x89, 0x50, 0x4E, 0x47, 0x00, 0xFF];
        var written = new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);
        MockFileSystem fileSystem = Create(expected);
        fileSystem.File.SetLastWriteTimeUtc(Source, written.AddDays(1));

        // Act
        fileSystem.File.CopyContent(Source, Destination, written);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(fileSystem.File.ReadAllBytes(Destination)).IsEquivalentTo(expected);
            await Assert.That(fileSystem.File.GetLastWriteTimeUtc(Destination)).IsEqualTo(written);
        }
    }

    /// <summary>
    ///     Verifies that an existing destination is replaced whole, so that a longer earlier copy leaves no
    ///     bytes behind the new contents.
    /// </summary>
    [Test]
    public async Task CopyContent_Test_LongerDestination_IsReplacedWhole()
    {
        // Arrange
        byte[] expected = [0x01, 0x02];
        MockFileSystem fileSystem = Create(expected);
        fileSystem.File.WriteAllBytes(Destination, [0x0A, 0x0B, 0x0C, 0x0D]);

        // Act
        fileSystem.File.CopyContent(Source, Destination, DateTime.UnixEpoch);

        // Assert
        await Assert.That(fileSystem.File.ReadAllBytes(Destination)).IsEquivalentTo(expected);
    }

    /// <summary>
    ///     Verifies that the read-only attribute of the source stays on the source and does not reach the copy.
    /// </summary>
    [Test]
    public async Task CopyContent_Test_ReadOnlySource_YieldsAWritableCopy()
    {
        // Arrange
        MockFileSystem fileSystem = Create([0x01]);
        fileSystem.File.SetAttributes(Source, FileAttributes.ReadOnly);

        // Act
        fileSystem.File.CopyContent(Source, Destination, DateTime.UnixEpoch);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(fileSystem.File.GetAttributes(Destination).HasFlag(FileAttributes.ReadOnly)).IsFalse();
            await Assert.That(fileSystem.File.GetAttributes(Source).HasFlag(FileAttributes.ReadOnly)).IsTrue();
        }
    }

    #endregion // CopyContent Tests

    #region ReadText Tests

    /// <summary>
    ///     Verifies that a source in each encoding whose byte order mark StreamReader detects, and one in UTF-8
    ///     without a mark, reads as File.ReadAllText reads it, without the mark.
    /// </summary>
    [Test]
    [MethodDataSource(nameof(Encodings))]
    public async Task ReadText_Test_Encoding_MatchesReadAllText(Encoding encoding)
    {
        // Arrange
        byte[] bytes = [.. encoding.GetPreamble(), .. encoding.GetBytes(Text)];
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile(Page, bytes);

        // Act
        string text = fileSystem.File.ReadText(Page);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(text).IsEqualTo(Text);
            await Assert.That(text).IsEqualTo(ReadAllText(bytes));
        }
    }

    /// <summary>
    ///     Verifies that a byte UTF-8 does not allow and a sequence cut off at the end each read as U+FFFD, as
    ///     File.ReadAllText reads them, so that a stray byte in a source does not fail the build.
    /// </summary>
    [Test]
    public async Task ReadText_Test_InvalidUtf8_ReadsAsReplacementCharacters()
    {
        // Arrange
        byte[] bytes = [0x61, 0xFF, 0x62, 0xE2, 0x82];
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile(Page, bytes);

        // Act
        string text = fileSystem.File.ReadText(Page);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(text).IsEqualTo("a\U0000FFFDb\U0000FFFD");
            await Assert.That(text).IsEqualTo(ReadAllText(bytes));
        }
    }

    /// <summary>
    ///     Verifies that an empty source reads as empty text.
    /// </summary>
    [Test]
    public async Task ReadText_Test_EmptyFile_ReadsAsEmptyText()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile(Page, []);

        // Act
        string text = fileSystem.File.ReadText(Page);

        // Assert
        await Assert.That(text).IsEqualTo(string.Empty);
    }

    #endregion // ReadText Tests

    #region Helpers

    /// <summary>
    ///     Every encoding whose byte order mark StreamReader detects, each writing its mark, and UTF-8 writing
    ///     none.
    /// </summary>
    public static IEnumerable<Func<Encoding>> Encodings()
    {
        yield return () => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        yield return () => new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        yield return () => new UnicodeEncoding(bigEndian: false, byteOrderMark: true);
        yield return () => new UnicodeEncoding(bigEndian: true, byteOrderMark: true);
        yield return () => new UTF32Encoding(bigEndian: false, byteOrderMark: true);
        yield return () => new UTF32Encoding(bigEndian: true, byteOrderMark: true);
    }

    // The reader File.ReadAllText reads through, over the bytes themselves, so that the reference does not
    // depend on the mock.
    private static string ReadAllText(byte[] bytes)
    {
        using var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        return reader.ReadToEnd();
    }

    // The destination directory exists, as the build creates it before any copy.
    private static MockFileSystem Create(byte[] contents)
    {
        var fileSystem = new MockFileSystem();

        fileSystem.AddFile(Source, contents);
        fileSystem.Directory.CreateDirectory(Path.GetDirectoryName(Destination) ?? string.Empty);

        return fileSystem;
    }

    #endregion
}
