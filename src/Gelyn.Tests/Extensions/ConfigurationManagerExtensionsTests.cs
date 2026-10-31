using System;
using System.Collections.Generic;
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
            [OverrideOptions.Content] = "pages",
            [OverrideOptions.Output] = "public",
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
            await Assert.That(options.ContentDirectory).IsEqualTo("pages");
            await Assert.That(options.OutputDirectory).IsEqualTo("public");
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

    #endregion
}
