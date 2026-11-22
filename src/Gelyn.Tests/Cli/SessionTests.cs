using System;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

using Gelyn.Cli;
using Gelyn.Commands;
using Gelyn.Core.Internals;
using Gelyn.Model;
using Gelyn.Model.Options;
using Gelyn.Tests.Fixtures;

using Microsoft.Extensions.Logging;

namespace Gelyn.Tests.Cli;

/// <summary>
///     Unit tests for the <see cref="Session"/> class.
/// </summary>
/// <remarks>
///     Every session reads its configuration file from a directory on disk, which the fixture supplies, and
///     every command line is parsed by the tree the tool composes.
/// </remarks>
[Category(Categories.Integration)]
public class SessionTests
{
    #region Options Tests

    /// <summary>
    ///     Verifies that a file that does not parse is raised on the first read of the options, which reports
    ///     the configuration error, so that an option which never reads them still runs.
    /// </summary>
    [Test]
    public async Task Options_Test_FileThatDoesNotParse_IsRaisedOnTheFirstRead()
    {
        // Arrange
        using DiskFixture fixture = DiskFixture.Create()
            .WriteOutside(ConfigurationFile.DefaultFileName, """{ "Title": """);

        // Act
        using Session session = Open(fixture.Root, Parse("build"));
        ConfigurationFileStatus probed = session.Settings.Probe();
        ConfigurationException exception = Assert.Throws<ConfigurationException>(() => _ = session.Options);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(probed.Exists).IsTrue();
            await Assert.That(probed.Failure).IsNotNull();
            await Assert.That(ExitCodes.For(exception)).IsEqualTo(ExitCodes.ConfigurationError);
        }
    }

    /// <summary>
    ///     Verifies that a file requested through <c>--config</c> is resolved against the content root, so that
    ///     a relative path climbing out of it is loaded from the file it names.
    /// </summary>
    [Test]
    public async Task Options_Test_RelativePathOutsideTheContentRoot_IsLoaded()
    {
        // Arrange
        using DiskFixture fixture = DiskFixture.Create()
            .WriteOutside("shared.json", """{ "Title": "Shared" }""");

        string site = Directory.CreateDirectory(Path.Combine(fixture.Root, "site")).FullName;

        // Act
        using Session session = Open(site, Parse("build", ConfigurationFile.OptionName, "../shared.json"));
        SiteOptions options = session.Options;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(session.Settings.Path).IsEqualTo(Path.Combine(fixture.Root, "shared.json"));
            await Assert.That(session.Settings.Optional).IsFalse();
            await Assert.That(options.Title).IsEqualTo("Shared");
        }
    }

    /// <summary>
    ///     Verifies that the options are loaded once, so that every service of the run reads the same values.
    /// </summary>
    [Test]
    public async Task Options_Test_SecondRead_ReturnsTheSameInstance()
    {
        // Arrange
        using var fixture = DiskFixture.Create();
        using Session session = Open(fixture.Root, Parse("build"));

        // Act
        SiteOptions first = session.Options;
        SiteOptions second = session.Options;

        // Assert
        await Assert.That(second).IsSameReferenceAs(first);
    }

    #endregion // Options Tests

    #region Service Tests

    /// <summary>
    ///     Verifies that <c>--verbosity</c> sets the level the services log at, and that its default keeps the
    ///     warnings and hides the timings.
    /// </summary>
    [Test]
    [Arguments(new[] { "build", "--verbosity", "Debug" }, true)]
    [Arguments(new[] { "build" }, false)]
    public async Task Loggers_Test_Verbosity_SetsTheMinimumLevel(string[] args, bool debug)
    {
        // Arrange
        using var fixture = DiskFixture.Create();

        // Act
        using Session session = Open(fixture.Root, Parse(args));
        ILogger logger = session.Loggers.CreateLogger<SessionTests>();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(logger.IsEnabled(LogLevel.Debug)).IsEqualTo(debug);
            await Assert.That(logger.IsEnabled(LogLevel.Warning)).IsTrue();
        }
    }

    /// <summary>
    ///     Verifies that the builder is constructed once, so that the watcher rebuilds through the builder the
    ///     first build ran on.
    /// </summary>
    [Test]
    public async Task Builder_Test_SecondRead_ReturnsTheSameInstance()
    {
        // Arrange
        using var fixture = DiskFixture.Create();
        using Session session = Open(fixture.Root, Parse("serve"));

        // Act and Assert
        await Assert.That(session.Builder).IsSameReferenceAs(session.Builder);
    }

    /// <summary>
    ///     Verifies that a disposed session refuses its options and its loggers, which the command it belonged
    ///     to has released.
    /// </summary>
    [Test]
    public async Task Dispose_Test_DisposedSession_RefusesItsMembers()
    {
        // Arrange
        using var fixture = DiskFixture.Create();
        Session session = Open(fixture.Root, Parse("build"));

        // Act
        session.Dispose();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(() => _ = session.Options).Throws<ObjectDisposedException>();
            await Assert.That(() => _ = session.Loggers).Throws<ObjectDisposedException>();
        }
    }

    #endregion // Service Tests

    #region Helpers

    private static ParseResult Parse(params string[] args) =>
        GelynCommand.Create(static _ => throw new InvalidOperationException("The tree is parsed here and never invoked.")).Parse(args);

    private static Session Open(string contentRoot, ParseResult parsed) =>
        Session.Open(parsed, contentRoot, CapturedConsole.Create(TextWriter.Null), CapturedConsole.Create(TextWriter.Null), Stopwatch.GetTimestamp());

    #endregion
}
