using System;
using System.IO;
using System.Threading.Tasks;

using Gelyn.Extensions;
using Gelyn.Tests.Fixtures;

using Microsoft.Extensions.Hosting;

namespace Gelyn.Tests.Extensions;

/// <summary>
///     Unit tests for the <see cref="HostExtensions"/> class.
/// </summary>
public class HostExtensionsTests
{
    private const string ContentRootVariable = "DOTNET_CONTENTROOT";

    #region Content Root Tests

    /// <summary>
    ///     Verifies that the content root is the directory the tool was invoked from, so that an installed
    ///     tool resolves <c>content/</c> and <c>gelyn.json</c> against the caller and not against itself.
    /// </summary>
    /// <remarks>
    ///     An empty host builder resolves the content root to the binary's own directory, which this guards
    ///     against by moving the working directory away from it for the duration of the test. The directory is
    ///     process-wide, so the test runs alone.
    /// </remarks>
    [Test]
    [NotInParallel]
    public async Task CreateLeanApplicationBuilder_Test_NoContentRootGiven_ResolvesToTheWorkingDirectory()
    {
        // Arrange
        using var fixture = DiskFixture.Create();
        string original = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(fixture.Root);

            // Read back rather than taken from the fixture, because the file system may report the directory
            // under a resolved spelling, as macOS does for its temporary directory.
            string expected = Directory.GetCurrentDirectory();

            // Act
            HostApplicationBuilder builder = HostExtensions.CreateLeanApplicationBuilder();
            string actual = Normalize(builder.Environment.ContentRootPath);

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
    public async Task CreateLeanApplicationBuilder_Test_ContentRootGiven_IsHonored()
    {
        // Arrange
        using var fixture = DiskFixture.Create();

        try
        {
            Environment.SetEnvironmentVariable(ContentRootVariable, fixture.Root);

            // Act
            HostApplicationBuilder builder = HostExtensions.CreateLeanApplicationBuilder();

            // Assert
            await Assert.That(Normalize(builder.Environment.ContentRootPath)).IsEqualTo(Normalize(fixture.Root));
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
    public async Task CreateLeanApplicationBuilder_Test_RelativeContentRoot_ResolvesAgainstTheWorkingDirectory()
    {
        // Arrange
        using var fixture = DiskFixture.Create();
        string original = Directory.GetCurrentDirectory();

        // The host requires the content root to exist.
        Directory.CreateDirectory(Path.Combine(fixture.Root, "site"));

        try
        {
            Directory.SetCurrentDirectory(fixture.Root);
            Environment.SetEnvironmentVariable(ContentRootVariable, "site");
            string expected = Path.Combine(Directory.GetCurrentDirectory(), "site");

            // Act
            HostApplicationBuilder builder = HostExtensions.CreateLeanApplicationBuilder();
            string actual = Normalize(builder.Environment.ContentRootPath);

            // Assert
            await Assert.That(actual).IsEqualTo(Normalize(expected));
        }
        finally
        {
            Environment.SetEnvironmentVariable(ContentRootVariable, null);
            Directory.SetCurrentDirectory(original);
        }
    }

    #endregion // Content Root Tests

    #region Helpers

    private static string Normalize(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    #endregion
}
