using System;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

using Gelyn.Cli;
using Gelyn.Commands;
using Gelyn.Core.Internals;
using Gelyn.Exceptions;
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
    public async Task Log_Test_Verbosity_SetsTheMinimumLevel(string[] args, bool debug)
    {
        // Arrange
        using var fixture = DiskFixture.Create();

        // Act
        using Session session = Open(fixture.Root, Parse(args));
        ILogger logger = session.Log.CreateLogger<SessionTests>();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(logger.IsEnabled(LogLevel.Debug)).IsEqualTo(debug);
            await Assert.That(logger.IsEnabled(LogLevel.Warning)).IsTrue();
        }
    }

    /// <summary>
    ///     Verifies that <c>--quiet</c> replaces standard output with a stream that discards its text, before and
    ///     after the subcommand alike, and that the session keeps the given stream without it.
    /// </summary>
    [Test]
    [Arguments(new[] { "build" }, false)]
    [Arguments(new[] { "build", "--quiet" }, true)]
    [Arguments(new[] { "--quiet", "serve" }, true)]
    public async Task StdOut_Test_Quiet_DiscardsTheOutput(string[] args, bool quiet)
    {
        // Arrange
        using var fixture = DiskFixture.Create();
        ConsoleStream stdOut = CapturedConsole.Create(TextWriter.Null);

        // Act
        using var session = Session.Open(Parse(args), fixture.Root, stdOut, CapturedConsole.Create(TextWriter.Null), Stopwatch.GetTimestamp());

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(session.StdOut.IsDiscarded).IsEqualTo(quiet);
            await Assert.That(ReferenceEquals(session.StdOut, stdOut)).IsEqualTo(!quiet);
        }
    }

    /// <summary>
    ///     Verifies that the startup record reaches the error console when the first service asks for a logger,
    ///     in the documented line format.
    /// </summary>
    [Test]
    public async Task Log_Test_FirstRead_WritesTheStartupRecord()
    {
        // Arrange
        using var fixture = DiskFixture.Create();
        using var stdErr = new StringWriter();

        // Act
        using (Session session = Open(fixture.Root, Parse("build", "--verbosity", "Debug"), CapturedConsole.Create(stdErr)))
            _ = session.Log;

        // Assert
        await Assert.That(stdErr.ToString()).Matches(@"^\d\d:\d\d:\d\d\.\d{3} dbug: Gelyn\.Cli\.Session\[1006\] Started in [\d.]+ ms\r?\n$");
    }

    /// <summary>
    ///     Verifies that the level of a record is colored on a console that renders ANSI colors and plain on one
    ///     that does not, which is what a redirected error stream is.
    /// </summary>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task Log_Test_Colors_FollowTheErrorConsole(bool useColors)
    {
        // Arrange
        using var fixture = DiskFixture.Create();
        using var stdErr = new StringWriter();

        var console = new ConsoleStream(stdErr, isTerminal: useColors, useColors);

        // Act
        using (Session session = Open(fixture.Root, Parse("build", "--verbosity", "Debug"), console))
            _ = session.Log;

        // Assert
        await Assert.That(stdErr.ToString().Contains('\e', StringComparison.Ordinal)).IsEqualTo(useColors);
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
            await Assert.That(() => _ = session.Log).Throws<ObjectDisposedException>();
        }
    }

    #endregion // Service Tests

    #region Helpers

    private static ParseResult Parse(params string[] args) =>
        GelynCommand.Create(static _ => throw new InvalidOperationException("The tree is parsed here and never invoked.")).Parse(args);

    private static Session Open(string contentRoot, ParseResult parsed, ConsoleStream? stdErr = null) =>
        Session.Open(parsed, contentRoot, CapturedConsole.Create(TextWriter.Null), stdErr ?? CapturedConsole.Create(TextWriter.Null), Stopwatch.GetTimestamp());

    #endregion
}
