using System;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

using Gelyn.Abstractions;
using Gelyn.Cli;
using Gelyn.Commands;
using Gelyn.Model.Options;
using Gelyn.Tests.Fixtures;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gelyn.Tests.Cli;

/// <summary>
///     Unit tests for the <see cref="Session"/> class.
/// </summary>
/// <remarks>
///     Every session reads its configuration file from a directory on disk, which the fixture supplies, and
///     every command line is parsed by the tree the tool composes, so that a promotion follows every option the
///     tool declares.
/// </remarks>
[Category(Categories.Integration)]
public class SessionTests
{
    #region Override Promotion Tests

    /// <summary>
    ///     Verifies that each flag standing for one setting reaches that setting, so that a value given on
    ///     the command line outranks both the configuration file and the defaults.
    /// </summary>
    [Test]
    public async Task Overrides_Test_PromotedFlags_ReachTheSettingsTheyStandFor()
    {
        // Arrange
        using var fixture = DiskFixture.Create();

        // Act
        SiteOptions options = Open(
            fixture.Root,
            "serve",
            "--title",
            "From the flag",
            "--base-url",
            "https://example.com/gelyn",
            "--theme",
            "modern",
            "--feed",
            "true",
            "--sitemap",
            "true",
            "--content",
            "pages",
            "--output",
            "public",
            "--port",
            "8080");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(options.Title).IsEqualTo("From the flag");
            await Assert.That(options.BaseUrl).IsEqualTo(new Uri("https://example.com/gelyn"));
            await Assert.That(options.Theme).IsEqualTo("modern");
            await Assert.That(options.EnableFeed).IsTrue();
            await Assert.That(options.EnableSitemap).IsTrue();
            await Assert.That(options.ContentDirectory).IsEqualTo("pages");
            await Assert.That(options.OutputDirectory).IsEqualTo("public");
            await Assert.That(options.Port).IsEqualTo(8_080);
        }
    }

    /// <summary>
    ///     Verifies that a switch given <c>false</c> turns off a document the configuration file enables, so
    ///     that the flag overrides the setting in both directions.
    /// </summary>
    [Test]
    [Arguments(OverrideOptions.Feed, nameof(SiteOptions.EnableFeed))]
    [Arguments(OverrideOptions.Sitemap, nameof(SiteOptions.EnableSitemap))]
    public async Task Overrides_Test_FalseSwitch_DisablesAConfiguredDocument(string option, string setting)
    {
        // Arrange
        using DiskFixture fixture = DiskFixture.Create()
            .WriteOutside(ConfigurationFile.DefaultFileName, $$"""{ "{{setting}}": true }""");

        // Act
        SiteOptions options = Open(fixture.Root, "build", $"--{option}", "false");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(options.EnableFeed).IsFalse();
            await Assert.That(options.EnableSitemap).IsFalse();
        }
    }

    /// <summary>
    ///     Verifies that a value joined to its flag by a colon or an equals sign reaches the setting, both being
    ///     forms System.CommandLine accepts.
    /// </summary>
    [Test]
    [Arguments("--title:From the flag")]
    [Arguments("--title=From the flag")]
    public async Task Overrides_Test_JoinedValue_ReachesTheSetting(string argument)
    {
        // Arrange
        using var fixture = DiskFixture.Create();

        // Act
        SiteOptions options = Open(fixture.Root, "build", argument, "--output:public");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(options.Title).IsEqualTo("From the flag");
            await Assert.That(options.OutputDirectory).IsEqualTo("public");
        }
    }

    /// <summary>
    ///     Verifies that the flags a response file holds reach their settings, which System.CommandLine expands
    ///     before it parses.
    /// </summary>
    [Test]
    public async Task Overrides_Test_ResponseFile_ReachesTheSettings()
    {
        // Arrange
        using DiskFixture fixture = DiskFixture.Create()
            .WriteOutside("options.rsp", $"--title \"From the file\"{Environment.NewLine}--output public{Environment.NewLine}");

        // Act
        SiteOptions options = Open(fixture.Root, "build", $"@{Path.Combine(fixture.Root, "options.rsp")}");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(options.Title).IsEqualTo("From the file");
            await Assert.That(options.OutputDirectory).IsEqualTo("public");
        }
    }

    /// <summary>
    ///     Verifies that an option taking no value leaves the token after it to the option it belongs to.
    /// </summary>
    [Test]
    public async Task Overrides_Test_ValuelessOption_LeavesTheNextOptionItsValue()
    {
        // Arrange
        using var fixture = DiskFixture.Create();

        // Act
        SiteOptions options = Open(fixture.Root, "serve", "--launch-browser", "--port", "9000");

        // Assert
        await Assert.That(options.Port).IsEqualTo(9_000);
    }

    #endregion // Override Promotion Tests

    #region Exclude Promotion Tests

    /// <summary>
    ///     Verifies that a promoted exclude pattern is added to the configured list and does not overwrite
    ///     the entry it would otherwise share an index with.
    /// </summary>
    /// <remarks>
    ///     Configuration merges an array per index, so writing at index zero would yield the promoted pattern
    ///     alone and silently drop the configured one. Keys are compared numerically, which is what lets the
    ///     offset append.
    /// </remarks>
    [Test]
    public async Task Exclude_Test_PromotedPatterns_AreAppendedToTheConfiguredOnes()
    {
        // Arrange
        using DiskFixture fixture = DiskFixture.Create()
            .WriteOutside(ConfigurationFile.DefaultFileName, """{ "ExcludePatterns": ["from-file"] }""");

        // Act
        SiteOptions options = Open(fixture.Root, "build", "--exclude", "from-flag");

        // Assert
        await Assert.That(options.ExcludePatterns).IsEquivalentTo(["from-file", "from-flag"]);
    }

    /// <summary>
    ///     Verifies that one flag value carrying several separated patterns reaches the options as several
    ///     entries, which is the only shape available because the flag takes one string.
    /// </summary>
    [Test]
    public async Task Exclude_Test_SeparatedPatterns_ReachTheOptionsIndividually()
    {
        // Arrange
        using var fixture = DiskFixture.Create();

        // Act
        SiteOptions options = Open(fixture.Root, "build", "--exclude", $"*.tmp{OverrideOptions.ExcludeSeparator} drafts ");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(options.ExcludePatterns).Contains("*.tmp");
            await Assert.That(options.ExcludePatterns).Contains("drafts");
        }
    }

    #endregion // Exclude Promotion Tests

    #region Configuration File Tests

    /// <summary>
    ///     Verifies that a file that does not parse is held as the failure the first read of the services
    ///     raises, which reports the configuration error, so that an option which never reads them still runs.
    /// </summary>
    [Test]
    public async Task File_Test_FileThatDoesNotParse_IsHeldUntilTheServicesAreRead()
    {
        // Arrange
        using DiskFixture fixture = DiskFixture.Create()
            .WriteOutside(ConfigurationFile.DefaultFileName, """{ "Title": """);

        // Act
        using Session session = Open(fixture.Root, Parse("build"));
        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => _ = session.Services);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(session.File.Exists).IsTrue();
            await Assert.That(session.File.Failure).IsSameReferenceAs(exception);
            await Assert.That(ExitCodes.For(exception)).IsEqualTo(ExitCodes.ConfigurationError);
        }
    }

    /// <summary>
    ///     Verifies that a file requested through <c>--config</c> that does not exist is held as missing, which
    ///     reports the configuration error.
    /// </summary>
    [Test]
    public async Task File_Test_MissingRequestedFile_IsHeldAsNotFound()
    {
        // Arrange
        using var fixture = DiskFixture.Create();
        string path = Path.Combine(fixture.Root, "missing.json");

        // Act
        using Session session = Open(fixture.Root, Parse("build", ConfigurationFile.OptionName, path));
        FileNotFoundException exception = Assert.Throws<FileNotFoundException>(() => _ = session.Services);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(session.File.Path).IsEqualTo(path);
            await Assert.That(session.File.Optional).IsFalse();
            await Assert.That(session.File.Exists).IsFalse();
            await Assert.That(session.File.Failure).IsSameReferenceAs(exception);
            await Assert.That(ExitCodes.For(exception)).IsEqualTo(ExitCodes.ConfigurationError);
        }
    }

    /// <summary>
    ///     Verifies that a missing <c>gelyn.json</c> is no failure, because the defaults apply without it.
    /// </summary>
    [Test]
    public async Task File_Test_MissingDefaultFile_IsNoFailure()
    {
        // Arrange
        using var fixture = DiskFixture.Create();

        // Act
        using Session session = Open(fixture.Root, Parse("build"));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(session.File.Path).IsEqualTo(Path.Combine(fixture.Root, ConfigurationFile.DefaultFileName));
            await Assert.That(session.File.Optional).IsTrue();
            await Assert.That(session.File.Exists).IsFalse();
            await Assert.That(session.File.Failure).IsNull();
            await Assert.That(() => session.Services.GetRequiredService<SiteBuilderContract>()).ThrowsNothing();
        }
    }

    /// <summary>
    ///     Verifies that a relative path climbing out of the content root is loaded from the file it names.
    /// </summary>
    [Test]
    public async Task File_Test_RelativePathOutsideTheContentRoot_IsLoaded()
    {
        // Arrange
        using DiskFixture fixture = DiskFixture.Create()
            .WriteOutside("shared.json", """{ "Title": "Shared" }""");

        string site = Directory.CreateDirectory(Path.Combine(fixture.Root, "site")).FullName;

        // Act
        using Session session = Open(site, Parse("build", ConfigurationFile.OptionName, "../shared.json"));
        SiteOptions options = session.Services.GetRequiredService<IOptionsMonitor<SiteOptions>>().CurrentValue;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(session.File.Path).IsEqualTo(Path.Combine(fixture.Root, "shared.json"));
            await Assert.That(session.File.Exists).IsTrue();
            await Assert.That(session.File.Failure).IsNull();
            await Assert.That(options.Title).IsEqualTo("Shared");
        }
    }

    /// <summary>
    ///     Verifies that only the first load holds a failure, so that a reload under <c>serve</c> still reports
    ///     a file that broke after it loaded.
    /// </summary>
    [Test]
    public async Task File_Test_ReloadOfABrokenFile_Throws()
    {
        // Arrange
        using DiskFixture fixture = DiskFixture.Create()
            .WriteOutside(ConfigurationFile.DefaultFileName, """{ "Title": "Before" }""");

        using Session session = Open(fixture.Root, Parse("serve"));
        var configuration = session.Services.GetRequiredService<IConfigurationRoot>();

        fixture.WriteOutside(ConfigurationFile.DefaultFileName, """{ "Title": """);

        // Act and Assert
        using (Assert.Multiple())
        {
            await Assert.That(session.File.Failure).IsNull();
            await Assert.That(configuration.Reload).Throws<InvalidDataException>();
        }
    }

    #endregion // Configuration File Tests

    #region Environment Variable Tests

    /// <summary>
    ///     Verifies that an environment variable reaches a setting only under the <c>GELYN_</c> prefix, so that a
    ///     variable such as <c>LANGUAGE</c>, which Linux desktops set for gettext, leaves the site alone.
    /// </summary>
    /// <remarks>
    ///     Environment variables are process-wide, so the test runs alone and restores both values. Description
    ///     is the setting under test, because no test running beside this one reads it.
    /// </remarks>
    [Test]
    [NotInParallel]
    public async Task EnvironmentVariables_Test_OnlyPrefixedNames_ReachTheSettings()
    {
        // Arrange
        using var fixture = DiskFixture.Create();
        string? language = Environment.GetEnvironmentVariable("LANGUAGE");
        string? description = Environment.GetEnvironmentVariable("GELYN_DESCRIPTION");
        SiteOptions options;

        try
        {
            Environment.SetEnvironmentVariable("LANGUAGE", "de_DE:de");
            Environment.SetEnvironmentVariable("GELYN_DESCRIPTION", "From the environment");

            // Act
            options = Open(fixture.Root, "build");
        }
        finally
        {
            Environment.SetEnvironmentVariable("LANGUAGE", language);
            Environment.SetEnvironmentVariable("GELYN_DESCRIPTION", description);
        }

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(options.Language).IsEqualTo("en");
            await Assert.That(options.Description).IsEqualTo("From the environment");
        }
    }

    /// <summary>
    ///     Verifies that a variable under the <c>DOTNET_</c> prefix, which the host once seeded the configuration
    ///     with, no longer reaches a setting.
    /// </summary>
    /// <remarks>
    ///     Environment variables are process-wide, so the test runs alone and restores the value.
    /// </remarks>
    [Test]
    [NotInParallel]
    public async Task EnvironmentVariables_Test_HostPrefixedName_DoesNotReachTheSettings()
    {
        // Arrange
        using var fixture = DiskFixture.Create();
        string? title = Environment.GetEnvironmentVariable("DOTNET_TITLE");
        SiteOptions options;

        try
        {
            Environment.SetEnvironmentVariable("DOTNET_TITLE", "From the host");

            // Act
            options = Open(fixture.Root, "build");
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTNET_TITLE", title);
        }

        // Assert
        await Assert.That(options.Title).IsEqualTo(new SiteOptions().Title);
    }

    #endregion // Environment Variable Tests

    #region Service Tests

    /// <summary>
    ///     Verifies that <c>--verbosity</c> sets the level the services log at, and that its default keeps the
    ///     warnings and hides the timings.
    /// </summary>
    [Test]
    [Arguments(new[] { "build", "--verbosity", "Debug" }, true)]
    [Arguments(new[] { "build" }, false)]
    public async Task Services_Test_Verbosity_SetsTheMinimumLevel(string[] args, bool debug)
    {
        // Arrange
        using var fixture = DiskFixture.Create();

        // Act
        using Session session = Open(fixture.Root, Parse(args));
        var logger = session.Services.GetRequiredService<ILogger<SessionTests>>();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(logger.IsEnabled(LogLevel.Debug)).IsEqualTo(debug);
            await Assert.That(logger.IsEnabled(LogLevel.Warning)).IsTrue();
        }
    }

    /// <summary>
    ///     Verifies that a disposed session refuses its services, which the command it belonged to has released.
    /// </summary>
    [Test]
    public async Task Dispose_Test_DisposedSession_RefusesItsServices()
    {
        // Arrange
        using var fixture = DiskFixture.Create();
        Session session = Open(fixture.Root, Parse("build"));

        // Act
        session.Dispose();

        // Assert
        await Assert.That(() => _ = session.Services).Throws<ObjectDisposedException>();
    }

    #endregion // Service Tests

    #region Helpers

    private static ParseResult Parse(params string[] args) =>
        GelynCommand.Create(static _ => throw new InvalidOperationException("The tree is parsed here and never invoked.")).Parse(args);

    private static SiteOptions Open(string contentRoot, params string[] args)
    {
        using Session session = Open(contentRoot, Parse(args));

        return session.Services.GetRequiredService<IOptionsMonitor<SiteOptions>>().CurrentValue;
    }

    private static Session Open(string contentRoot, ParseResult parsed) =>
        Session.Open(parsed, contentRoot, CapturedConsole.Create(TextWriter.Null), CapturedConsole.Create(TextWriter.Null), Stopwatch.GetTimestamp());

    #endregion
}
