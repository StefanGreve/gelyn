using System;
using System.IO;
using System.Threading.Tasks;

using Gelyn.Core.Internals;
using Gelyn.Tests.Fixtures;

namespace Gelyn.Tests;

/// <summary>
///     End-to-end tests for the <see cref="Program"/> class, which run the tool against a site on disk.
/// </summary>
/// <remarks>
///     The exit code is decided by the catch block of <see cref="Program.Main"/>, which no test of a single
///     command reaches. The content root is read from <c>DOTNET_CONTENTROOT</c>, and the environment is
///     process-wide, so every test runs alone.
/// </remarks>
[NotInParallel]
[Category(Categories.Integration)]
public class ProgramTests
{
    private const string ContentRootVariable = "DOTNET_CONTENTROOT";

    #region Exit Code Tests

    /// <summary>
    ///     Verifies that a site that builds exits with success.
    /// </summary>
    [Test]
    public async Task Main_Test_SiteThatBuilds_ExitsWithSuccess()
    {
        // Arrange
        using DiskFixture fixture = DiskFixture.Create().Write("index.md", "# Home");

        // Act
        int exitCode = await RunAsync(fixture, "build");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(exitCode).IsEqualTo(ExitCodes.Success);
            await Assert.That(fixture.Holds(Path.Combine(fixture.Options.OutputDirectory, "index.html"))).IsTrue();
        }
    }

    /// <summary>
    ///     Verifies that a content directory without a landing page is rejected as a configuration error, as
    ///     the documentation states, and not as a failed build.
    /// </summary>
    [Test]
    public async Task Main_Test_MissingLandingPage_ExitsWithAConfigurationError()
    {
        // Arrange
        using DiskFixture fixture = DiskFixture.Create().Write("about.md", "# About");

        // Act
        int exitCode = await RunAsync(fixture, "build");

        // Assert
        await Assert.That(exitCode).IsEqualTo(ExitCodes.ConfigurationError);
    }

    /// <summary>
    ///     Verifies that a setting the validator rejects is a configuration error.
    /// </summary>
    [Test]
    public async Task Main_Test_InvalidSetting_ExitsWithAConfigurationError()
    {
        // Arrange
        using DiskFixture fixture = DiskFixture.Create().Write("index.md", "# Home");

        // Act
        int exitCode = await RunAsync(fixture, "build", "--base-url", "ftp://example.com");

        // Assert
        await Assert.That(exitCode).IsEqualTo(ExitCodes.ConfigurationError);
    }

    /// <summary>
    ///     Verifies that front matter that does not parse fails the build, which is not a configuration error.
    /// </summary>
    [Test]
    public async Task Main_Test_MalformedFrontMatter_ExitsWithAnError()
    {
        // Arrange
        using DiskFixture fixture = DiskFixture.Create().Write("index.md", "---\ntitle: [unclosed\n---\n\n# Home");

        // Act
        int exitCode = await RunAsync(fixture, "build");

        // Assert
        await Assert.That(exitCode).IsEqualTo(ExitCodes.Error);
    }

    /// <summary>
    ///     Verifies that an I/O failure while writing fails the build, which the build command reports itself.
    /// </summary>
    [Test]
    public async Task Main_Test_OutputDirectoryThatIsAFile_ExitsWithAnError()
    {
        // Arrange
        using DiskFixture fixture = DiskFixture.Create().Write("index.md", "# Home");

        Directory.Delete(fixture.OutputRoot);
        fixture.WriteOutside(fixture.Options.OutputDirectory, "not a directory");

        // Act
        int exitCode = await RunAsync(fixture, "build");

        // Assert
        await Assert.That(exitCode).IsEqualTo(ExitCodes.Error);
    }

    #endregion // Exit Code Tests

    #region Clean Tests

    /// <summary>
    ///     Verifies that <c>clean</c> removes the output directory and everything in it.
    /// </summary>
    [Test]
    public async Task Main_Test_Clean_RemovesTheOutputDirectory()
    {
        // Arrange
        using DiskFixture fixture = DiskFixture.Create().Write("index.md", "# Home");
        fixture.WriteOutside(Path.Combine(fixture.Options.OutputDirectory, "blog", "post.html"), "<p>Post</p>");

        // Act
        int exitCode = await RunAsync(fixture, "clean");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(exitCode).IsEqualTo(ExitCodes.Success);
            await Assert.That(Directory.Exists(fixture.OutputRoot)).IsFalse();
            await Assert.That(Directory.Exists(fixture.ContentRoot)).IsTrue();
        }
    }

    /// <summary>
    ///     Verifies that <c>clean</c> refuses an output directory that is the content directory, as a build
    ///     does, so that it never removes the sources.
    /// </summary>
    [Test]
    public async Task Main_Test_Clean_RefusesTheContentDirectory()
    {
        // Arrange
        using DiskFixture fixture = DiskFixture.Create().Write("index.md", "# Home");

        // Act
        int exitCode = await RunAsync(fixture, "clean", "--output", fixture.Options.ContentDirectory);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(exitCode).IsEqualTo(ExitCodes.ConfigurationError);
            await Assert.That(File.Exists(Path.Combine(fixture.ContentRoot, "index.md"))).IsTrue();
        }
    }

    #endregion // Clean Tests

    #region Helpers

    private static async Task<int> RunAsync(DiskFixture fixture, params string[] args)
    {
        string? original = Environment.GetEnvironmentVariable(ContentRootVariable);

        try
        {
            Environment.SetEnvironmentVariable(ContentRootVariable, fixture.Root);

            return await Program.Main(args);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ContentRootVariable, original);
        }
    }

    #endregion
}
