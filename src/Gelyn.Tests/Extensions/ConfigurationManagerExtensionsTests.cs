using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using Gelyn.Core.Internals;
using Gelyn.Extensions;
using Gelyn.Model;
using Gelyn.Model.Options;
using Gelyn.Tests.Fixtures;

using Microsoft.Extensions.Configuration;

namespace Gelyn.Tests.Extensions;

/// <summary>
///     Unit tests for the <see cref="ConfigurationManagerExtensions"/> class.
/// </summary>
public class ConfigurationManagerExtensionsTests
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
        ParseResult parsed = Parse(
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

        // Act
        SiteOptions options = Bind(parsed);

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
    /// <remarks>
    ///     The enabled setting is added ahead of <see cref="ConfigurationManagerExtensions.AddSiteConfiguration"/>,
    ///     which is the position the configuration file takes relative to the overrides.
    /// </remarks>
    [Test]
    [Arguments(OverrideOptions.Feed, nameof(SiteOptions.EnableFeed))]
    [Arguments(OverrideOptions.Sitemap, nameof(SiteOptions.EnableSitemap))]
    public async Task Overrides_Test_FalseSwitch_DisablesAConfiguredDocument(string option, string setting)
    {
        // Arrange
        ParseResult parsed = Parse("build", $"--{option}", "false");

        // Act
        SiteOptions options = Bind(parsed, new Dictionary<string, string?> { [setting] = "true" });

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(options.EnableFeed).IsFalse();
            await Assert.That(options.EnableSitemap).IsFalse();
        }
    }

    /// <summary>
    ///     Verifies that a value joined to its flag by a colon or an equals sign reaches the setting, both being
    ///     forms System.CommandLine accepts and the command line configuration provider did not.
    /// </summary>
    [Test]
    [Arguments("--title:From the flag")]
    [Arguments("--title=From the flag")]
    public async Task Overrides_Test_JoinedValue_ReachesTheSetting(string argument)
    {
        // Arrange
        ParseResult parsed = Parse("build", argument, "--output:public");

        // Act
        SiteOptions options = Bind(parsed);

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

        ParseResult parsed = Parse("build", $"@{Path.Combine(fixture.Root, "options.rsp")}");

        // Act
        SiteOptions options = Bind(parsed);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(options.Title).IsEqualTo("From the file");
            await Assert.That(options.OutputDirectory).IsEqualTo("public");
        }
    }

    /// <summary>
    ///     Verifies that an option taking no value leaves the token after it to the option it belongs to, where
    ///     the command line configuration provider paired the two.
    /// </summary>
    [Test]
    public async Task Overrides_Test_ValuelessOption_LeavesTheNextOptionItsValue()
    {
        // Arrange
        ParseResult parsed = Parse("serve", "--launch-browser", "--port", "9000");

        // Act
        SiteOptions options = Bind(parsed);

        // Assert
        await Assert.That(options.Port).IsEqualTo(9_000);
    }

    /// <summary>
    ///     Verifies that applying the overrides raises no reload, which the options monitor would answer by
    ///     binding and validating before <c>--help</c> or <c>--info</c> runs.
    /// </summary>
    [Test]
    public async Task ApplyOverrides_Test_Overrides_RaiseNoReload()
    {
        // Arrange
        var configuration = new ConfigurationManager();
        configuration.AddSiteConfiguration(requested: null);

        bool reloaded = false;
        ((IConfiguration)configuration).GetReloadToken().RegisterChangeCallback(_ => reloaded = true, state: null);

        // Act
        configuration.ApplyOverrides(Parse("build", "--title", "From the flag"));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(reloaded).IsFalse();
            await Assert.That(configuration[nameof(SiteOptions.Title)]).IsEqualTo("From the flag");
        }
    }

    /// <summary>
    ///     Verifies that applying the overrides to a configuration without the site sources is refused, since
    ///     no source exists for them to outrank the others from.
    /// </summary>
    [Test]
    public async Task ApplyOverrides_Test_WithoutTheSiteConfiguration_Throws()
    {
        // Arrange
        var configuration = new ConfigurationManager();

        // Act & Assert
        await Assert.That(() => configuration.ApplyOverrides(Parse("build"))).Throws<InvalidOperationException>();
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
    ///     offset append. Driven against the binder instead of through
    ///     <see cref="ConfigurationManagerExtensions.AddSiteConfiguration"/>, which would read whatever
    ///     <c>gelyn.json</c> the working directory happens to hold.
    /// </remarks>
    [Test]
    public async Task Exclude_Test_PromotedPatterns_AreAppendedToTheConfiguredOnes()
    {
        // Arrange
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ExcludePatterns:0"] = "from-file",
            })
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ExcludePatterns:{OverrideOptions.ExcludeOffset}"] = "from-flag",
            })
            .Build();

        var options = new SiteOptions();

        // Act
        configuration.Bind(options);

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
        ParseResult parsed = Parse("build", "--exclude", $"*.tmp{OverrideOptions.ExcludeSeparator} drafts ");

        // Act
        SiteOptions options = Bind(parsed);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(options.ExcludePatterns).Contains("*.tmp");
            await Assert.That(options.ExcludePatterns).Contains("drafts");
        }
    }

    #endregion // Exclude Promotion Tests

    #region Footer Link Tests

    /// <summary>
    ///     Verifies that indexed keys naming the members of a footer link reach the options as one link,
    ///     which is the shape a configuration file and the environment both flatten to.
    /// </summary>
    [Test]
    public async Task FooterLinks_Test_IndexedKeys_ReachTheOptions()
    {
        // Arrange
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FooterLinks:0:Label"] = "GitHub",
                ["FooterLinks:0:Url"] = "https://github.com/octocat",
            })
            .Build();

        var options = new SiteOptions();

        // Act
        configuration.Bind(options);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(options.FooterLinks).Count().IsEqualTo(1);
            await Assert.That(options.FooterLinks[0].Label).IsEqualTo("GitHub");
            await Assert.That(options.FooterLinks[0].Url).IsEqualTo(new Uri("https://github.com/octocat"));
        }
    }

    /// <summary>
    ///     Verifies that a configuration file setting the list to <c>null</c> leaves the default empty list in
    ///     place, which every consumer relies on by reading the list without a null check.
    /// </summary>
    /// <remarks>
    ///     Driven through the JSON provider, because how a <c>null</c> reaches the binder is that provider's
    ///     decision, and an in-memory key would not show it.
    /// </remarks>
    [Test]
    public async Task FooterLinks_Test_NullValue_LeavesTheListEmpty()
    {
        // Arrange
        using var json = new MemoryStream(Encoding.UTF8.GetBytes("""{ "FooterLinks": null }"""));

        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddJsonStream(json)
            .Build();

        var options = new SiteOptions();

        // Act
        configuration.Bind(options);

        // Assert
        await Assert.That(options.FooterLinks).IsNotNull().And.IsEmpty();
    }

    #endregion // Footer Link Tests

    #region Theme Tests

    /// <summary>
    ///     Verifies that a configuration file setting the theme to <c>null</c>, which the schema documents as
    ///     the default, applies none, instead of naming an empty theme the validator would reject.
    /// </summary>
    /// <remarks>
    ///     Driven through the JSON provider, because how a <c>null</c> reaches the binder is that provider's
    ///     decision, and an in-memory key would not show it.
    /// </remarks>
    [Test]
    public async Task Theme_Test_NullValue_AppliesNone()
    {
        // Arrange
        using var json = new MemoryStream(Encoding.UTF8.GetBytes("""{ "Theme": null }"""));

        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddJsonStream(json)
            .Build();

        var options = new SiteOptions();

        // Act
        configuration.Bind(options);

        // Assert
        await Assert.That(options.Theme).IsNull();
    }

    #endregion // Theme Tests

    #region Configuration File Tests

    /// <summary>
    ///     Verifies that a file that does not parse is reported instead of thrown, so that a command which
    ///     never reads the options still runs.
    /// </summary>
    [Test]
    public async Task ConfigurationFile_Test_FileThatDoesNotParse_IsReported()
    {
        // Arrange
        using DiskFixture fixture = DiskFixture.Create()
            .WriteOutside(ConfigurationFile.DefaultFileName, """{ "Title": """);

        string path = Path.Combine(fixture.Root, ConfigurationFile.DefaultFileName);
        var configuration = new ConfigurationManager();

        // Act
        ConfigurationFileStatus status = configuration.AddSiteConfiguration(path);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(status.Exists).IsTrue();
            await Assert.That(status.Failure).IsTypeOf<InvalidDataException>();
        }
    }

    /// <summary>
    ///     Verifies that a file requested through <c>--config</c> that does not exist is reported as missing.
    /// </summary>
    [Test]
    public async Task ConfigurationFile_Test_MissingRequestedFile_IsReported()
    {
        // Arrange
        using var fixture = DiskFixture.Create();
        string path = Path.Combine(fixture.Root, "missing.json");
        var configuration = new ConfigurationManager();

        // Act
        ConfigurationFileStatus status = configuration.AddSiteConfiguration(path);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(status.Path).IsEqualTo(path);
            await Assert.That(status.Optional).IsFalse();
            await Assert.That(status.Exists).IsFalse();
            await Assert.That(status.Failure).IsTypeOf<FileNotFoundException>();
        }
    }

    /// <summary>
    ///     Verifies that a missing <c>gelyn.json</c> is no failure, because the defaults apply without it.
    /// </summary>
    [Test]
    public async Task ConfigurationFile_Test_MissingDefaultFile_IsNoFailure()
    {
        // Arrange
        using var fixture = DiskFixture.Create();
        var configuration = new ConfigurationManager();
        configuration.SetBasePath(fixture.Root);

        // Act
        ConfigurationFileStatus status = configuration.AddSiteConfiguration(requested: null);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(status.Path).IsEqualTo(Path.Combine(fixture.Root, ConfigurationFile.DefaultFileName));
            await Assert.That(status.Optional).IsTrue();
            await Assert.That(status.Exists).IsFalse();
            await Assert.That(status.Failure).IsNull();
        }
    }

    /// <summary>
    ///     Verifies that a relative path climbing out of the base directory is loaded from the file it names.
    /// </summary>
    [Test]
    public async Task ConfigurationFile_Test_RelativePathOutsideTheBase_IsLoaded()
    {
        // Arrange
        using DiskFixture fixture = DiskFixture.Create()
            .WriteOutside("shared.json", """{ "Title": "Shared" }""");

        string site = Directory.CreateDirectory(Path.Combine(fixture.Root, "site")).FullName;
        var configuration = new ConfigurationManager();
        configuration.SetBasePath(site);

        // Act
        ConfigurationFileStatus status = configuration.AddSiteConfiguration("../shared.json");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(status.Path).IsEqualTo(Path.Combine(fixture.Root, "shared.json"));
            await Assert.That(status.Exists).IsTrue();
            await Assert.That(status.Failure).IsNull();
            await Assert.That(configuration[nameof(SiteOptions.Title)]).IsEqualTo("Shared");
        }
    }

    /// <summary>
    ///     Verifies that only the first load is deferred, so that a reload under <c>serve</c> still reports a
    ///     file that broke after it loaded.
    /// </summary>
    [Test]
    public async Task ConfigurationFile_Test_ReloadOfABrokenFile_Throws()
    {
        // Arrange
        using DiskFixture fixture = DiskFixture.Create()
            .WriteOutside(ConfigurationFile.DefaultFileName, """{ "Title": "Before" }""");

        string path = Path.Combine(fixture.Root, ConfigurationFile.DefaultFileName);
        var configuration = new ConfigurationManager();
        ConfigurationFileStatus status = configuration.AddSiteConfiguration(path);

        fixture.WriteOutside(ConfigurationFile.DefaultFileName, """{ "Title": """);

        // Act and Assert
        using (Assert.Multiple())
        {
            await Assert.That(status.Failure).IsNull();
            await Assert.That(() => ((IConfigurationRoot)configuration).Reload()).Throws<InvalidDataException>();
        }
    }

    /// <summary>
    ///     Verifies that the configuration reports the path the loader was given, whatever an earlier source holds
    ///     under the same key, because the watcher of <c>serve</c> reads the key to pick the file it watches.
    /// </summary>
    [Test]
    [Arguments("site.json")]
    [Arguments(null)]
    public async Task ConfigurationFile_Test_RequestedPath_OutranksAnEarlierKey(string? requested)
    {
        // Arrange
        var configuration = new ConfigurationManager();

        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [ConfigurationFile.PathKey] = "paired-by-the-provider.json",
        });

        // Act
        configuration.AddSiteConfiguration(requested);

        // Assert
        await Assert.That(configuration[ConfigurationFile.PathKey]).IsEqualTo(requested);
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
        string? language = Environment.GetEnvironmentVariable("LANGUAGE");
        string? description = Environment.GetEnvironmentVariable("GELYN_DESCRIPTION");
        var configuration = new ConfigurationManager();
        var options = new SiteOptions();

        try
        {
            Environment.SetEnvironmentVariable("LANGUAGE", "de_DE:de");
            Environment.SetEnvironmentVariable("GELYN_DESCRIPTION", "From the environment");

            // Act
            configuration.AddSiteConfiguration(requested: null);
            configuration.Bind(options);
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

    #endregion // Environment Variable Tests

    #region Helpers

    // Shaped like the real command tree, whose commands need services to construct.
    private static ParseResult Parse(params string[] args)
    {
        var build = new Command("build");
        var serve = new Command("serve");

        foreach (Option option in OverrideOptions.Definitions)
            build.Options.Add(option);

        foreach (Option option in OverrideOptions.Definitions)
            serve.Options.Add(option);

        serve.Options.Add(new Option<int>($"--{OverrideOptions.Port}"));
        serve.Options.Add(new Option<bool>("--launch-browser"));

        var root = new RootCommand();
        root.Subcommands.Add(build);
        root.Subcommands.Add(serve);

        return root.Parse(args);
    }

    // The configured values, if any, take the place of the configuration file.
    private static SiteOptions Bind(ParseResult parsed, Dictionary<string, string?>? configured = null)
    {
        var configuration = new ConfigurationManager();

        if (configured is not null) configuration.AddInMemoryCollection(configured);

        configuration.AddSiteConfiguration(requested: null);
        configuration.ApplyOverrides(parsed);

        var options = new SiteOptions();
        configuration.Bind(options);

        return options;
    }

    #endregion
}
