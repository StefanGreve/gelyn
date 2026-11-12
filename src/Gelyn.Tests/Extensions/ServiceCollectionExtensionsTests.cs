using System.IO;
using System.Threading.Tasks;

using Gelyn.Core.Internals;
using Gelyn.Extensions;
using Gelyn.Model;
using Gelyn.Model.Options;
using Gelyn.Tests.Stubs;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Gelyn.Tests.Extensions;

/// <summary>
///     Unit tests for the <see cref="ServiceCollectionExtensions"/> class.
/// </summary>
public class ServiceCollectionExtensionsTests
{
    #region Configuration File Tests

    /// <summary>
    ///     Verifies that a failure the first load held back fails the first read of the options with the
    ///     exception the loader raised, which reports the configuration error.
    /// </summary>
    [Test]
    public async Task AddConfigurationFile_Test_StatusWithFailure_FailsTheFirstRead()
    {
        // Arrange
        var failure = new InvalidDataException("Failed to load configuration from file 'gelyn.json'.");

        await using ServiceProvider provider = Build(new ConfigurationFileStatus { Exists = true, Failure = failure });
        var options = provider.GetRequiredService<IOptionsMonitor<SiteOptions>>();

        // Act
        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => _ = options.CurrentValue);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(exception).IsSameReferenceAs(failure);
            await Assert.That(ExitCodes.For(exception)).IsEqualTo(ExitCodes.ConfigurationError);
        }
    }

    /// <summary>
    ///     Verifies that a status without a failure leaves the options readable.
    /// </summary>
    [Test]
    public async Task AddConfigurationFile_Test_StatusWithoutFailure_LeavesTheOptionsReadable()
    {
        // Arrange
        await using ServiceProvider provider = Build(new ConfigurationFileStatus { Exists = true, Failure = null });
        var options = provider.GetRequiredService<IOptionsMonitor<SiteOptions>>();

        // Act and Assert
        await Assert.That(() => options.CurrentValue).ThrowsNothing();
    }

    #endregion // Configuration File Tests

    #region Helpers

    private static ServiceProvider Build(ConfigurationFileStatus file)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationManager());
        services.AddSingleton<IHostEnvironment>(new HostEnvironmentStub { ContentRootPath = Path.GetFullPath("/site") });
        services.AddConfigurationFile(file);
        services.AddSiteOptions();

        return services.BuildServiceProvider();
    }

    #endregion
}
