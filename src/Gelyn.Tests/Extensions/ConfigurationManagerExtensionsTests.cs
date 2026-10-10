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
    #region Exclude Promotion Tests

    /// <summary>
    ///     Verifies that a promoted exclude pattern is added to the configured list rather than overwriting
    ///     the entry it would otherwise share an index with.
    /// </summary>
    /// <remarks>
    ///     Configuration merges an array per index, so writing at index zero would yield the promoted pattern
    ///     alone and silently drop the configured one. Keys are compared numerically, which is what lets the
    ///     offset append. Driven against the binder rather than through
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

        SiteOptions options = new();

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
        ConfigurationManager configuration = new();

        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [OverrideOptions.Exclude] = $"*.tmp{OverrideOptions.ExcludeSeparator} drafts ",
        });

        SiteOptions options = new();

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
