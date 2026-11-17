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
            HostApplicationBuilder builder = Array.Empty<string>().CreateLeanApplicationBuilder();
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
    ///     Verifies that a content root given on the command line wins over the working directory, which is
    ///     the key the host reads out of the seeded configuration.
    /// </summary>
    [Test]
    public async Task CreateLeanApplicationBuilder_Test_ContentRootGiven_IsHonoured()
    {
        // Arrange
        using var fixture = DiskFixture.Create();
        string[] args = [$"--{HostDefaults.ContentRootKey}", fixture.Root];

        // Act
        HostApplicationBuilder builder = args.CreateLeanApplicationBuilder();

        // Assert
        await Assert.That(Normalize(builder.Environment.ContentRootPath)).IsEqualTo(Normalize(fixture.Root));
    }

    /// <summary>
    ///     Verifies that a relative content root resolves against the working directory, not against the
    ///     directory the tool is installed in.
    /// </summary>
    /// <remarks>
    ///     The working directory is process-wide, so the test runs alone.
    /// </remarks>
    [Test]
    [NotInParallel]
    public async Task CreateLeanApplicationBuilder_Test_RelativeContentRoot_ResolvesAgainstTheWorkingDirectory()
    {
        // Arrange
        using var fixture = DiskFixture.Create();
        string original = Directory.GetCurrentDirectory();
        string[] args = [$"--{HostDefaults.ContentRootKey}", "site"];

        // The host requires the content root to exist.
        Directory.CreateDirectory(Path.Combine(fixture.Root, "site"));

        try
        {
            Directory.SetCurrentDirectory(fixture.Root);
            string expected = Path.Combine(Directory.GetCurrentDirectory(), "site");

            // Act
            HostApplicationBuilder builder = args.CreateLeanApplicationBuilder();
            string actual = Normalize(builder.Environment.ContentRootPath);

            // Assert
            await Assert.That(actual).IsEqualTo(Normalize(expected));
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
        }
    }

    #endregion // Content Root Tests

    #region Helpers

    private static string Normalize(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    #endregion
}
