using System.IO;
using System.Threading.Tasks;

using Gelyn.Cli;

namespace Gelyn.Tests.Cli;

/// <summary>
///     Unit tests for the <see cref="ConfigurationFile"/> class.
/// </summary>
[Category(Categories.Internal)]
public class ConfigurationFileTests
{
    #region Resolve Tests

    /// <summary>
    ///     Verifies that a command line without <c>--config</c> reads the default file of the content root.
    /// </summary>
    [Test]
    public async Task Resolve_Test_NoPathRequested_IsTheDefaultFileOfTheContentRoot()
    {
        // Act
        string path = ConfigurationFile.Resolve(Path.GetFullPath("/site"), requested: null);

        // Assert
        await Assert.That(path).IsEqualTo(Path.Combine(Path.GetFullPath("/site"), ConfigurationFile.DefaultFileName));
    }

    /// <summary>
    ///     Verifies that a relative path resolves against the content root, and may lead out of it.
    /// </summary>
    [Test]
    [Arguments("site.json", "/site/site.json")]
    [Arguments("../shared.json", "/shared.json")]
    public async Task Resolve_Test_RelativePath_ResolvesAgainstTheContentRoot(string requested, string expected)
    {
        // Act
        string path = ConfigurationFile.Resolve(Path.GetFullPath("/site"), requested);

        // Assert
        await Assert.That(path).IsEqualTo(Path.GetFullPath(expected));
    }

    /// <summary>
    ///     Verifies that an absolute path is taken as given, wherever the content root is.
    /// </summary>
    [Test]
    public async Task Resolve_Test_AbsolutePath_IsKept()
    {
        // Arrange
        string requested = Path.GetFullPath("/elsewhere/site.json");

        // Act
        string path = ConfigurationFile.Resolve(Path.GetFullPath("/site"), requested);

        // Assert
        await Assert.That(path).IsEqualTo(requested);
    }

    #endregion // Resolve Tests
}
