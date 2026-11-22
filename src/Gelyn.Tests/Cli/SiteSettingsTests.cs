using System;
using System.CommandLine;
using System.IO;
using System.Threading.Tasks;

using Gelyn.Cli;
using Gelyn.Commands;
using Gelyn.Core.Internals;
using Gelyn.Model;
using Gelyn.Model.Options;
using Gelyn.Tests.Fixtures;

using Testably.Abstractions.Testing;

namespace Gelyn.Tests.Cli;

/// <summary>
///     Unit tests for the <see cref="SiteSettings"/> class.
/// </summary>
/// <remarks>
///     Every configuration file lives in an in-memory file system, and every command line is parsed by the
///     tree the tool composes, so that an override follows every option the tool declares.
/// </remarks>
[Category(Categories.Internal)]
public class SiteSettingsTests
{
    private static readonly string Root = Path.GetFullPath("/site");

    #region Override Tests

    /// <summary>
    ///     Verifies that each flag standing for one setting reaches that setting, so that a value given on
    ///     the command line outranks both the configuration file and the defaults.
    /// </summary>
    [Test]
    public async Task Load_Test_PromotedFlags_ReachTheSettingsTheyStandFor()
    {
        // Act
        SiteOptions options = Load(
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
    public async Task Load_Test_FalseSwitch_DisablesAConfiguredDocument(string option, string setting)
    {
        // Act
        SiteOptions options = LoadFrom($$"""{ "{{setting}}": true }""", "build", $"--{option}", "false");

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
    public async Task Load_Test_JoinedValue_ReachesTheSetting(string argument)
    {
        // Act
        SiteOptions options = Load("build", argument, "--output:public");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(options.Title).IsEqualTo("From the flag");
            await Assert.That(options.OutputDirectory).IsEqualTo("public");
        }
    }

    /// <summary>
    ///     Verifies that the flags a response file holds reach their settings, which System.CommandLine expands
    ///     before it parses, reading the file from the disk the parser runs on.
    /// </summary>
    [Test]
    public async Task Load_Test_ResponseFile_ReachesTheSettings()
    {
        // Arrange
        using DiskFixture fixture = DiskFixture.Create()
            .WriteOutside("options.rsp", $"--title \"From the file\"{Environment.NewLine}--output public{Environment.NewLine}");

        // Act
        SiteOptions options = Load("build", $"@{Path.Combine(fixture.Root, "options.rsp")}");

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
    public async Task Load_Test_ValuelessOption_LeavesTheNextOptionItsValue()
    {
        // Act
        SiteOptions options = Load("serve", "--launch-browser", "--port", "9000");

        // Assert
        await Assert.That(options.Port).IsEqualTo(9_000);
    }

    /// <summary>
    ///     Verifies that a base URL the flag carries is converted as the binder converts one from the file, so
    ///     that an empty or malformed value is a configuration error and not a crash.
    /// </summary>
    [Test]
    [Arguments("")]
    [Arguments("http://[")]
    public async Task Load_Test_BaseUrlFlagThatIsNoUrl_IsAConfigurationError(string value)
    {
        // Act
        ConfigurationException exception = Assert.Throws<ConfigurationException>(() => Load("build", "--base-url", value));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(exception.Failures).Count().IsEqualTo(1);
            await Assert.That(exception.Failures[0]).Contains(nameof(SiteOptions.BaseUrl));
            await Assert.That(ExitCodes.For(exception)).IsEqualTo(ExitCodes.ConfigurationError);
        }
    }

    #endregion // Override Tests

    #region Exclude Tests

    /// <summary>
    ///     Verifies that a promoted exclude pattern is added to the configured list instead of replacing it.
    /// </summary>
    [Test]
    public async Task Load_Test_PromotedPatterns_AreAppendedToTheConfiguredOnes()
    {
        // Act
        SiteOptions options = LoadFrom("""{ "ExcludePatterns": ["from-file"] }""", "build", "--exclude", "from-flag");

        // Assert
        await Assert.That(options.ExcludePatterns).IsEquivalentTo(["from-file", "from-flag"]);
    }

    /// <summary>
    ///     Verifies that one flag value carrying several separated patterns reaches the options as several
    ///     entries, which is the only shape available because the flag takes one string.
    /// </summary>
    [Test]
    public async Task Load_Test_SeparatedPatterns_ReachTheOptionsIndividually()
    {
        // Act
        SiteOptions options = Load("build", "--exclude", $"*.tmp{OverrideOptions.ExcludeSeparator} drafts ");

        // Assert
        await Assert.That(options.ExcludePatterns).IsEquivalentTo(["*.tmp", "drafts"]);
    }

    /// <summary>
    ///     Verifies that the flag appends after a variable has replaced an entry of the file by its index, so
    ///     that the three layers combine in their documented order.
    /// </summary>
    /// <remarks>
    ///     Environment variables are process-wide, so the test runs alone and restores the value.
    /// </remarks>
    [Test]
    [NotInParallel]
    public async Task Load_Test_PromotedPatterns_AppendAfterTheVariables()
    {
        // Arrange
        const string variable = "GELYN_EXCLUDEPATTERNS__0";
        string? previous = Environment.GetEnvironmentVariable(variable);
        SiteOptions options;

        try
        {
            Environment.SetEnvironmentVariable(variable, "from-variable");

            // Act
            options = LoadFrom("""{ "ExcludePatterns": ["from-file"] }""", "build", "--exclude", "from-flag");
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, previous);
        }

        // Assert
        await Assert.That(options.ExcludePatterns).IsEquivalentTo(["from-variable", "from-flag"]);
    }

    #endregion // Exclude Tests

    #region Configuration File Tests

    /// <summary>
    ///     Verifies that a file that does not parse is a configuration error that names the file and the position
    ///     of the error.
    /// </summary>
    [Test]
    public async Task Load_Test_FileThatDoesNotParse_IsAConfigurationError()
    {
        // Arrange
        SiteSettings settings = Create(WithFile("""{ "Title": """), "build");

        // Act
        ConfigurationException exception = Assert.Throws<ConfigurationException>(() => settings.Load());

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(exception.Failures).Count().IsEqualTo(1);
            await Assert.That(exception.Failures[0]).Contains(settings.Path).And.Contains("LineNumber");
            await Assert.That(ExitCodes.For(exception)).IsEqualTo(ExitCodes.ConfigurationError);
        }
    }

    /// <summary>
    ///     Verifies that a file requested through <c>--config</c> that does not exist is a configuration error
    ///     that names the file.
    /// </summary>
    [Test]
    public async Task Load_Test_MissingRequestedFile_IsAConfigurationError()
    {
        // Arrange
        string path = Path.Combine(Root, "missing.json");
        SiteSettings settings = Create(new MockFileSystem(), "build", ConfigurationFile.OptionName, path);

        // Act
        ConfigurationException exception = Assert.Throws<ConfigurationException>(() => settings.Load());

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(settings.Path).IsEqualTo(path);
            await Assert.That(settings.Optional).IsFalse();
            await Assert.That(exception.Failures).IsEquivalentTo([$"The configuration file '{path}' was not found and is not optional."]);
            await Assert.That(ExitCodes.For(exception)).IsEqualTo(ExitCodes.ConfigurationError);
        }
    }

    /// <summary>
    ///     Verifies that a missing <c>gelyn.json</c> is no failure, because the defaults apply without it.
    /// </summary>
    [Test]
    public async Task Load_Test_MissingDefaultFile_AppliesTheDefaults()
    {
        // Arrange
        SiteSettings settings = Create(new MockFileSystem(), "build");

        // Act
        SiteOptions options = settings.Load();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(settings.Path).IsEqualTo(Path.Combine(Root, ConfigurationFile.DefaultFileName));
            await Assert.That(settings.Optional).IsTrue();
            await Assert.That(options.Title).IsEqualTo(new SiteOptions().Title);
        }
    }

    /// <summary>
    ///     Verifies that a relative path climbing out of the content root is loaded from the file it names.
    /// </summary>
    [Test]
    public async Task Load_Test_RelativePathOutsideTheContentRoot_IsLoaded()
    {
        // Arrange
        string shared = Path.GetFullPath("/shared.json");
        SiteSettings settings = Create(WithFile("""{ "Title": "Shared" }""", shared), "build", ConfigurationFile.OptionName, "../shared.json");

        // Act
        SiteOptions options = settings.Load();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(settings.Path).IsEqualTo(shared);
            await Assert.That(options.Title).IsEqualTo("Shared");
        }
    }

    /// <summary>
    ///     Verifies that every load reads the file again, so that a reload under <c>serve</c> reports a file
    ///     that broke after it loaded.
    /// </summary>
    [Test]
    public async Task Load_Test_FileBrokenAfterALoad_FailsTheNextLoad()
    {
        // Arrange
        MockFileSystem fileSystem = WithFile("""{ "Title": "Before" }""");
        SiteSettings settings = Create(fileSystem, "serve");
        SiteOptions before = settings.Load();

        fileSystem.File.WriteAllText(settings.Path, """{ "Title": """);

        // Act and Assert
        using (Assert.Multiple())
        {
            await Assert.That(before.Title).IsEqualTo("Before");
            await Assert.That(settings.Load).Throws<ConfigurationException>();
        }
    }

    /// <summary>
    ///     Verifies that every failed rule is reported at once, so that a file with several unusable values is
    ///     fixed in one pass.
    /// </summary>
    [Test]
    public async Task Load_Test_SeveralRejectedValues_AreReportedTogether()
    {
        // Act
        ConfigurationException exception = Assert.Throws<ConfigurationException>(() => LoadFrom("""{ "Title": "", "Language": "" }""", "build"));

        // Assert
        await Assert.That(exception.Failures).IsEquivalentTo([
            $"{nameof(SiteOptions.Title)} must not be empty.",
            $"{nameof(SiteOptions.Language)} must not be empty.",
        ]);
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
    public async Task Load_Test_OnlyPrefixedVariables_ReachTheSettings()
    {
        // Arrange
        string? language = Environment.GetEnvironmentVariable("LANGUAGE");
        string? description = Environment.GetEnvironmentVariable("GELYN_DESCRIPTION");
        SiteOptions options;

        try
        {
            Environment.SetEnvironmentVariable("LANGUAGE", "de_DE:de");
            Environment.SetEnvironmentVariable("GELYN_DESCRIPTION", "From the environment");

            // Act
            options = Load("build");
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
    ///     with, does not reach a setting.
    /// </summary>
    /// <remarks>
    ///     Environment variables are process-wide, so the test runs alone and restores the value.
    /// </remarks>
    [Test]
    [NotInParallel]
    public async Task Load_Test_HostPrefixedVariable_DoesNotReachTheSettings()
    {
        // Arrange
        string? title = Environment.GetEnvironmentVariable("DOTNET_TITLE");
        SiteOptions options;

        try
        {
            Environment.SetEnvironmentVariable("DOTNET_TITLE", "From the host");

            // Act
            options = Load("build");
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTNET_TITLE", title);
        }

        // Assert
        await Assert.That(options.Title).IsEqualTo(new SiteOptions().Title);
    }

    #endregion // Environment Variable Tests

    #region Bind Tests

    /// <summary>
    ///     Verifies that a file without a giscus section leaves the backend unset.
    /// </summary>
    [Test]
    public async Task Load_Test_AbsentGiscusSection_LeavesTheSettingNull()
    {
        // Act
        SiteOptions options = LoadFrom("""{ "Title": "Notebook" }""", "build");

        // Assert
        await Assert.That(options.Giscus).IsNull();
    }

    /// <summary>
    ///     Verifies that a giscus section binds each of its four members.
    /// </summary>
    [Test]
    public async Task Load_Test_GiscusSection_BindsEveryMember()
    {
        // Act
        SiteOptions options = LoadFrom(
            """
            {
              "Giscus": {
                "Repo": "octocat/blog",
                "RepoId": "R_kgDOxxxxxxx",
                "Category": "Comments",
                "CategoryId": "DIC_kwDOxxxxxxxxxxxx"
              }
            }
            """,
            "build");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(options.Giscus).IsNotNull();
            await Assert.That(options.Giscus?.Repo).IsEqualTo("octocat/blog");
            await Assert.That(options.Giscus?.RepoId).IsEqualTo("R_kgDOxxxxxxx");
            await Assert.That(options.Giscus?.Category).IsEqualTo("Comments");
            await Assert.That(options.Giscus?.CategoryId).IsEqualTo("DIC_kwDOxxxxxxxxxxxx");
        }
    }

    /// <summary>
    ///     Verifies that a footer link reaches the options with both of its members, which is the shape a
    ///     configuration file and the environment both flatten to.
    /// </summary>
    [Test]
    public async Task Load_Test_FooterLinks_ReachTheOptions()
    {
        // Act
        SiteOptions options = LoadFrom("""{ "FooterLinks": [{ "Label": "GitHub", "Url": "https://github.com/octocat" }] }""", "build");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(options.FooterLinks).Count().IsEqualTo(1);
            await Assert.That(options.FooterLinks[0].Label).IsEqualTo("GitHub");
            await Assert.That(options.FooterLinks[0].Url).IsEqualTo(new Uri("https://github.com/octocat"));
        }
    }

    /// <summary>
    ///     Verifies that a file setting the footer links to <c>null</c> leaves the default empty list in place,
    ///     which every consumer relies on by reading the list without a null check.
    /// </summary>
    [Test]
    public async Task Load_Test_NullFooterLinks_LeaveTheListEmpty()
    {
        // Act
        SiteOptions options = LoadFrom("""{ "FooterLinks": null }""", "build");

        // Assert
        await Assert.That(options.FooterLinks).IsNotNull().And.IsEmpty();
    }

    /// <summary>
    ///     Verifies that a file setting the theme to <c>null</c>, which the schema documents as the default,
    ///     applies none, instead of naming an empty theme the validator would reject.
    /// </summary>
    [Test]
    public async Task Load_Test_NullTheme_AppliesNone()
    {
        // Act
        SiteOptions options = LoadFrom("""{ "Theme": null }""", "build");

        // Assert
        await Assert.That(options.Theme).IsNull();
    }

    /// <summary>
    ///     Verifies that a value the binder cannot convert to the type of its setting is a configuration error
    ///     that names the setting.
    /// </summary>
    [Test]
    public async Task Load_Test_ValueTheBinderCannotConvert_IsAConfigurationError()
    {
        // Act
        ConfigurationException exception = Assert.Throws<ConfigurationException>(() => LoadFrom("""{ "BaseUrl": "http://[" }""", "build"));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(exception.Failures).Count().IsEqualTo(1);
            await Assert.That(exception.Failures[0]).Contains(nameof(SiteOptions.BaseUrl));
            await Assert.That(ExitCodes.For(exception)).IsEqualTo(ExitCodes.ConfigurationError);
        }
    }

    #endregion // Bind Tests

    #region Probe Tests

    /// <summary>
    ///     Verifies that a probe reports whether the file exists and whether it can be read, and holds the
    ///     failure of a read instead of raising it.
    /// </summary>
    [Test]
    [Arguments(null, true, false, false)]
    [Arguments("""{ "Title": "Read" }""", true, true, false)]
    [Arguments("""{ "Title": """, true, true, true)]
    [Arguments(null, false, false, true)]
    public async Task Probe_Test_FileState_IsReportedWithoutThrowing(string? contents, bool optional, bool exists, bool fails)
    {
        // Arrange
        MockFileSystem fileSystem = contents is null ? new MockFileSystem() : WithFile(contents);
        string path = Path.Combine(Root, ConfigurationFile.DefaultFileName);
        SiteSettings settings = optional ? Create(fileSystem, "build") : Create(fileSystem, "build", ConfigurationFile.OptionName, path);

        // Act
        ConfigurationFileStatus status = settings.Probe();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(status.Path).IsEqualTo(path);
            await Assert.That(status.Optional).IsEqualTo(optional);
            await Assert.That(status.Exists).IsEqualTo(exists);
            await Assert.That(status.Failure is not null).IsEqualTo(fails);
        }
    }

    /// <summary>
    ///     Verifies that a probe reads the file and binds nothing, so that a value a rule rejects is no failure
    ///     of the file.
    /// </summary>
    [Test]
    public async Task Probe_Test_RejectedValue_IsNoFailureOfTheFile()
    {
        // Arrange
        SiteSettings settings = Create(WithFile("""{ "Title": "" }"""), "build");

        // Act
        ConfigurationFileStatus status = settings.Probe();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(status.Exists).IsTrue();
            await Assert.That(status.Failure).IsNull();
        }
    }

    #endregion // Probe Tests

    #region Helpers

    private static ParseResult Parse(params string[] args) =>
        GelynCommand.Create(static _ => throw new InvalidOperationException("The tree is parsed here and never invoked.")).Parse(args);

    // Resolved as the session resolves it, from --config when given and from the content root otherwise.
    private static SiteSettings Create(MockFileSystem fileSystem, params string[] args)
    {
        ParseResult parsed = Parse(args);
        string? requested = parsed.GetValue<string>(ConfigurationFile.OptionName);

        return new SiteSettings(Root, ConfigurationFile.Resolve(Root, requested), optional: requested is null, parsed, fileSystem);
    }

    private static SiteOptions Load(params string[] args) =>
        Create(new MockFileSystem(), args).Load();

    private static SiteOptions LoadFrom(string json, params string[] args) =>
        Create(WithFile(json), args).Load();

    private static MockFileSystem WithFile(string json, string? path = null)
    {
        var fileSystem = new MockFileSystem();

        fileSystem.AddFile(path ?? Path.Combine(Root, ConfigurationFile.DefaultFileName), json);

        return fileSystem;
    }

    #endregion
}
