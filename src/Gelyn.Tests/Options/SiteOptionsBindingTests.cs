using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Gelyn.Extensions;
using Gelyn.Model.Options;
using Gelyn.Tests.Stubs;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Gelyn.Tests.Options;

/// <summary>
///     Unit tests for how the configuration binder the tool is built with fills <see cref="SiteOptions"/>.
/// </summary>
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

    #endregion // Bind Tests

    #region Helpers

    private static async Task<SiteOptions> BindAsync(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationManager();

        configuration.AddInMemoryCollection(settings);

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IHostEnvironment>(new HostEnvironmentStub { ContentRootPath = Path.GetFullPath("/site") });
        services.AddSiteOptions();

        await using ServiceProvider provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IOptions<SiteOptions>>().Value;
    }

    #endregion
}
