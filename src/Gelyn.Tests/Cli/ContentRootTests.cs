using System;
using System.IO;
using System.Threading.Tasks;

using Gelyn.Cli;
using Gelyn.Tests.Fixtures;

namespace Gelyn.Tests.Cli;

/// <summary>
///     Unit tests for the <see cref="ContentRoot"/> class.
/// </summary>
[Category(Categories.Integration)]
public class ContentRootTests
{
    private const string ContentRootVariable = "DOTNET_CONTENTROOT";

    #region Resolve Tests

    /// <summary>
    ///     Verifies that the content root is the directory the tool was invoked from, so that an installed
    ///     tool resolves <c>content/</c> and <c>gelyn.json</c> against the caller and not against itself.
    /// </summary>
    /// <remarks>
    ///     The working directory is moved away from the binary's own for the duration of the test, so that
    ///     the two cannot coincide by chance. The directory is process-wide, so the test runs alone.
    /// </remarks>
    [Test]
    [NotInParallel]
    public async Task Resolve_Test_NoContentRootGiven_ResolvesToTheWorkingDirectory()
    {
        // Arrange
        using var fixture = DiskFixture.Create();
        string original = Directory.GetCurrentDirectory();
        string? contentRoot = Environment.GetEnvironmentVariable(ContentRootVariable);

        try
        {
            // A content root inherited from the shell would win over the working directory.
            Environment.SetEnvironmentVariable(ContentRootVariable, null);
            Directory.SetCurrentDirectory(fixture.Root);

            // Read back rather than taken from the fixture, because the file system may report the directory
            // under a resolved spelling, as macOS does for its temporary directory.
            string expected = Directory.GetCurrentDirectory();

            // Act
            string actual = Normalize(ContentRoot.Resolve());

            // Assert
            using (Assert.Multiple())
            {
                await Assert.That(actual).IsEqualTo(Normalize(expected));
                await Assert.That(actual).IsNotEqualTo(Normalize(AppContext.BaseDirectory));
            }
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
            Environment.SetEnvironmentVariable(ContentRootVariable, contentRoot);
        }
    }

    /// <summary>
    ///     Verifies that a content root given in <c>DOTNET_CONTENTROOT</c> wins over the working directory.
    /// </summary>
    /// <remarks>
    ///     The environment is process-wide, so the test runs alone.
    /// </remarks>
    [Test]
    [NotInParallel]
    public async Task Resolve_Test_ContentRootGiven_IsHonored()
    {
        // Arrange
        using var fixture = DiskFixture.Create();

        try
        {
            Environment.SetEnvironmentVariable(ContentRootVariable, fixture.Root);

            // Act
            string actual = Normalize(ContentRoot.Resolve());

            // Assert
            await Assert.That(actual).IsEqualTo(Normalize(fixture.Root));
        }
        finally
        {
            Environment.SetEnvironmentVariable(ContentRootVariable, null);
        }
    }

    /// <summary>
    ///     Verifies that a relative content root resolves against the working directory, not against the
    ///     directory the tool is installed in.
    /// </summary>
    /// <remarks>
    ///     The working directory and the environment are process-wide, so the test runs alone.
    /// </remarks>
    [Test]
    [NotInParallel]
    public async Task Resolve_Test_RelativeContentRoot_ResolvesAgainstTheWorkingDirectory()
    {
        // Arrange
        using var fixture = DiskFixture.Create();
        string original = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(fixture.Root);
            Environment.SetEnvironmentVariable(ContentRootVariable, "site");
            string expected = Path.Combine(Directory.GetCurrentDirectory(), "site");

            // Act
            string actual = Normalize(ContentRoot.Resolve());

            // Assert
            await Assert.That(actual).IsEqualTo(Normalize(expected));
        }
        finally
        {
            Environment.SetEnvironmentVariable(ContentRootVariable, null);
            Directory.SetCurrentDirectory(original);
        }
    }

    #endregion // Resolve Tests

    #region Helpers

    private static string Normalize(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    #endregion
}
