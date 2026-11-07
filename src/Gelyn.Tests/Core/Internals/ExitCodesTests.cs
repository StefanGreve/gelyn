using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Gelyn.Core.Internals;
using Gelyn.Extensions;
using Gelyn.Model.Options;
using Gelyn.Tests.Stubs;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Gelyn.Tests.Core.Internals;

/// <summary>
///     Unit tests for the <see cref="ExitCodes"/> class.
/// </summary>
public class ExitCodesTests
{
    #region For Tests

    /// <summary>
    ///     Verifies that every shape of a rejected configuration reports the configuration error.
    /// </summary>
    [Test]
    [MethodDataSource(nameof(ConfigurationFailures))]
    public async Task For_Test_RejectedConfiguration_ReportsTheConfigurationError(Exception exception)
    {
        // Act
        int code = ExitCodes.For(exception);

        // Assert
        await Assert.That(code).IsEqualTo(ExitCodes.ConfigurationError);
    }

    /// <summary>
    ///     Verifies that any other failure reports the plain error, an invalid operation without a parse
    ///     failure inside included.
    /// </summary>
    [Test]
    [MethodDataSource(nameof(OtherFailures))]
    public async Task For_Test_OtherFailure_ReportsTheError(Exception exception)
    {
        // Act
        int code = ExitCodes.For(exception);

        // Assert
        await Assert.That(code).IsEqualTo(ExitCodes.Error);
    }

    /// <summary>
    ///     Verifies that the binder the tool is built with throws the shape the mapping rests on.
    /// </summary>
    [Test]
    public async Task For_Test_ValueTheBinderCannotConvert_ReportsTheConfigurationError()
    {
        // Arrange
        var configuration = new ConfigurationManager();

        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [nameof(SiteOptions.BaseUrl)] = "http://[",
        });

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IHostEnvironment>(new HostEnvironmentStub { ContentRootPath = Path.GetFullPath("/site") });
        services.AddSiteOptions();

        await using ServiceProvider provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<SiteOptions>>();

        // Act
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => _ = options.CurrentValue);

        // Assert
        await Assert.That(ExitCodes.For(exception)).IsEqualTo(ExitCodes.ConfigurationError);
    }

    #endregion // For Tests

    #region Helpers

    /// <summary>
    ///     One exception per shape of a rejected configuration.
    /// </summary>
    public static IEnumerable<Func<Exception>> ConfigurationFailures()
    {
        yield return () => new OptionsValidationException(Microsoft.Extensions.Options.Options.DefaultName, typeof(SiteOptions), ["BaseUrl must be an absolute http or https URL."]);
        yield return () => new FileNotFoundException("The configuration file 'nope.json' was not found and is not optional.");
        yield return () => new FileNotFoundException("No landing page found at 'content/index.md'.");
        yield return () => new InvalidDataException("Failed to load configuration from file 'gelyn.json'.");
        yield return () => new InvalidOperationException("Failed to convert configuration value 'yes' at 'EnableFeed' to type 'System.Boolean'.", new FormatException());
        yield return () => new InvalidOperationException("Failed to convert configuration value 'http://[' at 'BaseUrl' to type 'System.Uri'.", new UriFormatException());
    }

    /// <summary>
    ///     Failures of a build, and invalid operations that are not the binder's.
    /// </summary>
    public static IEnumerable<Func<Exception>> OtherFailures()
    {
        yield return () => new IOException("The process cannot access the file because it is being used by another process.");
        yield return () => new InvalidOperationException("A listed document always declares a date.");
        yield return () => new InvalidOperationException("No service for type 'Gelyn.Services.SiteBuilder' has been registered.", new IOException());
        yield return () => new UnauthorizedAccessException("Access to the path is denied.");
    }

    #endregion
}
