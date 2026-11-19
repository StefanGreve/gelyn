using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Gelyn.Core.Internals;
using Gelyn.Tests.Fixtures;

using Testably.Abstractions.Testing;

namespace Gelyn.Tests.Core.Internals;

/// <summary>
///     Unit tests for the <see cref="FileUtils"/> class.
/// </summary>
[Category(Categories.Internal)]
public class FileUtilsTests
{
    private static readonly string Source = Path.GetFullPath("/site/content/logo.png");
    private static readonly string Destination = Path.GetFullPath("/site/_site/logo.png");

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
        FileUtils.CopyContent(fileSystem, Source, Destination, written);

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
        FileUtils.CopyContent(fileSystem, Source, Destination, DateTime.UnixEpoch);

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
        FileUtils.CopyContent(fileSystem, Source, Destination, DateTime.UnixEpoch);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(fileSystem.File.GetAttributes(Destination).HasFlag(FileAttributes.ReadOnly)).IsFalse();
            await Assert.That(fileSystem.File.GetAttributes(Source).HasFlag(FileAttributes.ReadOnly)).IsTrue();
        }
    }

    #endregion // CopyContent Tests

    #region WriteUtf8 Tests

    /// <summary>
    ///     Verifies that a text longer than the 8,192 characters <c>File.WriteAllText</c> writes per call, holding
    ///     two-, three- and four-byte characters, reaches the file as its UTF-8 encoding without a byte order mark.
    /// </summary>
    [Test]
    public async Task WriteUtf8_Test_LongMultiByteText_IsWrittenAsUtf8()
    {
        // Arrange
        string text = string.Concat(Enumerable.Repeat("Gr\U000000FC\U000000DFe, \U000020AC und \U0001F600. ", 1_000));
        MockFileSystem fileSystem = Create([]);

        // Act
        FileUtils.WriteUtf8(fileSystem, Destination, text);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(text.Length).IsGreaterThan(8_192);
            await Assert.That(fileSystem.File.ReadAllBytes(Destination)).IsEquivalentTo(Encoding.UTF8.GetBytes(text));
        }
    }

    /// <summary>
    ///     Verifies that an existing file is replaced whole, so that a longer earlier page leaves no bytes behind
    ///     the new contents.
    /// </summary>
    [Test]
    public async Task WriteUtf8_Test_LongerFile_IsReplacedWhole()
    {
        // Arrange
        MockFileSystem fileSystem = Create([]);
        fileSystem.File.WriteAllBytes(Destination, [0x0A, 0x0B, 0x0C, 0x0D]);

        // Act
        FileUtils.WriteUtf8(fileSystem, Destination, "ab");

        // Assert
        await Assert.That(fileSystem.File.ReadAllBytes(Destination)).IsEquivalentTo(new byte[] { 0x61, 0x62 });
    }

    /// <summary>
    ///     Verifies that a text holding an unpaired surrogate fails before the file is opened, so that the file
    ///     keeps what it held.
    /// </summary>
    [Test]
    public async Task WriteUtf8_Test_UnpairedSurrogate_LeavesTheFileAsItWas()
    {
        // Arrange
        byte[] expected = [0x0A, 0x0B];
        MockFileSystem fileSystem = Create([]);
        fileSystem.File.WriteAllBytes(Destination, expected);
        string text = "a" + (char)0xD800;

        // Act and Assert
        await Assert.That(() => FileUtils.WriteUtf8(fileSystem, Destination, text)).Throws<EncoderFallbackException>();
        await Assert.That(fileSystem.File.ReadAllBytes(Destination)).IsEquivalentTo(expected);
    }

    #endregion // WriteUtf8 Tests

    #region Helpers

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
