using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using Gelyn.Extensions;
using Gelyn.Model;
using Gelyn.Model.Options;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Gelyn.Tests.Options;

/// <summary>
///     Unit tests for how the configuration binder the tool is built with fills <see cref="SiteOptions"/>.
/// </summary>
[Category(Categories.Internal)]
public class SiteOptionsBindingTests
{
    #region Bind Tests

    /// <summary>
    ///     Verifies that a configuration without a giscus section leaves the backend unset.
    /// </summary>
    [Test]
    public async Task Bind_Test_AbsentGiscusSection_LeavesTheSettingNull()
    {
        // Act
        SiteOptions options = await BindAsync(new Dictionary<string, string?>
        {
            [nameof(SiteOptions.Title)] = "Notebook",
        });

        // Assert
        await Assert.That(options.Giscus).IsNull();
    }

    /// <summary>
    ///     Verifies that a giscus section binds each of its four members.
    /// </summary>
    [Test]
    public async Task Bind_Test_GiscusSection_BindsEveryMember()
    {
        // Act
        SiteOptions options = await BindAsync(new Dictionary<string, string?>
        {
            ["Giscus:Repo"] = "octocat/blog",
            ["Giscus:RepoId"] = "R_kgDOxxxxxxx",
            ["Giscus:Category"] = "Comments",
            ["Giscus:CategoryId"] = "DIC_kwDOxxxxxxxxxxxx",
        });

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
    ///     Verifies that indexed keys naming the members of a footer link reach the options as one link,
    ///     which is the shape a configuration file and the environment both flatten to.
    /// </summary>
    [Test]
    public async Task Bind_Test_IndexedFooterLinkKeys_ReachTheOptions()
    {
        // Act
        SiteOptions options = await BindAsync(new Dictionary<string, string?>
        {
            ["FooterLinks:0:Label"] = "GitHub",
            ["FooterLinks:0:Url"] = "https://github.com/octocat",
        });

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(options.FooterLinks).Count().IsEqualTo(1);
            await Assert.That(options.FooterLinks[0].Label).IsEqualTo("GitHub");
            await Assert.That(options.FooterLinks[0].Url).IsEqualTo(new Uri("https://github.com/octocat"));
        }
    }

    /// <summary>
    ///     Verifies that a configuration file setting the footer links to <c>null</c> leaves the default empty
    ///     list in place, which every consumer relies on by reading the list without a null check.
    /// </summary>
    [Test]
    public async Task Bind_Test_NullFooterLinks_LeaveTheListEmpty()
    {
        // Act
        SiteOptions options = await BindJsonAsync("""{ "FooterLinks": null }""");

        // Assert
        await Assert.That(options.FooterLinks).IsNotNull().And.IsEmpty();
    }

    /// <summary>
    ///     Verifies that a configuration file setting the theme to <c>null</c>, which the schema documents as
    ///     the default, applies none, instead of naming an empty theme the validator would reject.
    /// </summary>
    [Test]
    public async Task Bind_Test_NullTheme_AppliesNone()
    {
        // Act
        SiteOptions options = await BindJsonAsync("""{ "Theme": null }""");

        // Assert
        await Assert.That(options.Theme).IsNull();
    }

    #endregion // Bind Tests

    #region Helpers

    private static Task<SiteOptions> BindAsync(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(settings);

        return ResolveOptionsAsync(configuration);
    }

    // Read through the JSON provider, because how a null reaches the binder is that provider's decision.
    private static Task<SiteOptions> BindJsonAsync(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var configuration = new ConfigurationManager();
        configuration.AddJsonStream(stream);

        return ResolveOptionsAsync(configuration);
    }

    private static async Task<SiteOptions> ResolveOptionsAsync(ConfigurationManager configuration)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSiteOptions(Path.GetFullPath("/site"), new ConfigurationFileStatus { Path = Path.GetFullPath("/site/gelyn.json"), Optional = true, Exists = false, Failure = null });

        await using ServiceProvider provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IOptions<SiteOptions>>().Value;
    }

    #endregion
}
