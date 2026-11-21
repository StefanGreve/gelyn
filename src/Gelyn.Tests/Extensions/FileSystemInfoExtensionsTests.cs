using System.IO;
using System.IO.Abstractions;
using System.Threading.Tasks;

using Gelyn.Extensions;
using Gelyn.Tests.Fixtures;
using Gelyn.Tests.Stubs;

using Testably.Abstractions.Testing;

namespace Gelyn.Tests.Extensions;

/// <summary>
///     Unit tests for the <see cref="FileSystemInfoExtensions"/> class.
/// </summary>
[Category(Categories.Internal)]
public class FileSystemInfoExtensionsTests
{
    #region Link Tests

    /// <summary>
    ///     Verifies that a symbolic link is recognized as one.
    /// </summary>
    [Test]
    public async Task IsLink_Test_SymbolicLink_IsALink()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        string target = fileSystem.Path.GetFullPath("target.md");
        string link = fileSystem.Path.GetFullPath("link.md");

        fileSystem.AddFile(target, "# Target");
        fileSystem.File.CreateSymbolicLink(link, target);

        // Act
        IFileSystemInfo entry = fileSystem.FileInfo.New(link);

        // Assert
        await Assert.That(entry.IsLink).IsTrue();
    }

    /// <summary>
    ///     Verifies that an ordinary file is not a link.
    /// </summary>
    [Test]
    public async Task IsLink_Test_OrdinaryFile_IsNoLink()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        string path = fileSystem.Path.GetFullPath("page.md");

        fileSystem.AddFile(path, "# Page");

        // Act
        IFileSystemInfo entry = fileSystem.FileInfo.New(path);

        // Assert
        await Assert.That(entry.IsLink).IsFalse();
    }

    /// <summary>
    ///     Verifies that a reparse point without a link target, such as a deduplicated file on Windows, is not
    ///     a link, which is why the attribute alone does not decide.
    /// </summary>
    [Test]
    public async Task IsLink_Test_ReparsePointWithoutTarget_IsNoLink()
    {
        // Arrange
        // Stubbed, because the mock reports the attribute for a link target alone, as the operating systems do.
        var entry = new FileSystemInfoStub { Attributes = FileAttributes.ReparsePoint };

        // Act
        bool isLink = entry.IsLink;

        // Assert
        await Assert.That(isLink).IsFalse();
    }

    #endregion
}
