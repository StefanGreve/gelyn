using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using Gelyn.Core.Internals;
using Gelyn.Extensions;
using Gelyn.Model.Options;

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
    /// <remarks>
    ///     The promoted values are added after the file and the environment, which is what makes them win.
    ///     Driven through <see cref="ConfigurationManagerExtensions.AddSiteConfiguration"/>, because the
    ///     promotion is what is under test, and not the binding that follows it.
    /// </remarks>
    [Test]
    public async Task Overrides_Test_PromotedFlags_ReachTheSettingsTheyStandFor()
    {
        // Arrange
        var configuration = new ConfigurationManager();

        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [OverrideOptions.Title] = "From the flag",
            [OverrideOptions.BaseUrl] = "https://example.com/gelyn",
            [OverrideOptions.Theme] = "modern",
            [OverrideOptions.Feed] = "true",
            [OverrideOptions.Sitemap] = "true",
            [OverrideOptions.Content] = "pages",
            [OverrideOptions.Output] = "public",
            [OverrideOptions.Port] = "8080",
        });

        var options = new SiteOptions();

        // Act
        configuration.AddSiteConfiguration([]);
        configuration.Bind(options);

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
    ///     which is the position the configuration file takes relative to the promoted values.
    /// </remarks>
    [Test]
    [Arguments(OverrideOptions.Feed, nameof(SiteOptions.EnableFeed))]
    [Arguments(OverrideOptions.Sitemap, nameof(SiteOptions.EnableSitemap))]
    public async Task Overrides_Test_FalseSwitch_DisablesAConfiguredDocument(string option, string setting)
    {
        // Arrange
        var configuration = new ConfigurationManager();

        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [setting] = "true",
            [option] = "false",
        });

        var options = new SiteOptions();

        // Act
        configuration.AddSiteConfiguration([]);
        configuration.Bind(options);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(options.EnableFeed).IsFalse();
            await Assert.That(options.EnableSitemap).IsFalse();
        }
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
    ///     entries, which is the only shape available because the command line provider promotes one flag to
    ///     one string.
    /// </summary>
    [Test]
    public async Task Exclude_Test_SeparatedPatterns_ReachTheOptionsIndividually()
    {
        // Arrange
        var configuration = new ConfigurationManager();

        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [OverrideOptions.Exclude] = $"*.tmp{OverrideOptions.ExcludeSeparator} drafts ",
        });

        var options = new SiteOptions();

        // Act
        configuration.AddSiteConfiguration([]);
        configuration.Bind(options);

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
}
