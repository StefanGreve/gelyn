using System.IO.Abstractions;

using Gelyn.Abstractions;
using Gelyn.Core.Internals;
using Gelyn.Model;
using Gelyn.Model.Options;
using Gelyn.Services;
using Gelyn.Validators;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Spectre.Console;

namespace Gelyn.Extensions;

/// <summary>
///     Provides extension methods for <see cref="IServiceCollection"/>.
/// </summary>
internal static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Registers <see cref="SiteOptions"/> together with the validator that checks it.
    /// </summary>
    /// <remarks>
    ///     Bound to the root of the configuration and not to a section, because an empty path binds the
    ///     whole configuration and <c>gelyn.json</c> therefore needs no wrapping object. Validation is not
    ///     wired to startup, so it runs when the options are first read.
    /// </remarks>
    /// <param name="services">
    ///     The collection to add the registrations to.
    /// </param>
    /// <param name="contentRoot">
    ///     The directory the validator resolves the directory settings against.
    /// </param>
    /// <param name="file">
    ///     The configuration file of the run, whose location the validator checks against the output directory.
    /// </param>
    /// <returns>
    ///     The same <see cref="IServiceCollection"/> instance, so that calls can be chained.
    /// </returns>
    public static IServiceCollection AddSiteOptions(this IServiceCollection services, string contentRoot, ConfigurationFileStatus file)
    {
        services.AddSingleton<IValidateOptions<SiteOptions>>(new SiteOptionsValidator(contentRoot, file));

        services.AddOptions<SiteOptions>()
            .BindConfiguration(string.Empty);

        return services;
    }

    /// <summary>
    ///     Registers the services that discover content, generate the site and preview it.
    /// </summary>
    /// <param name="services">
    ///     The collection to add the registrations to.
    /// </param>
    /// <param name="contentRoot">
    ///     The directory every relative setting is resolved against.
    /// </param>
    /// <returns>
    ///     The same <see cref="IServiceCollection"/> instance, so that calls can be chained.
    /// </returns>
    public static IServiceCollection AddServices(this IServiceCollection services, string contentRoot)
    {
        services.TryAddSingleton<ContentPipelineContract>(provider =>
            new ContentPipeline(contentRoot, provider.GetRequiredService<IFileSystem>())
        );

        services.TryAddSingleton<SiteBuilderContract>(provider => new SiteBuilder(
            provider.GetRequiredService<ContentPipelineContract>(),
            contentRoot,
            provider.GetRequiredService<IOptionsMonitor<SiteOptions>>(),
            provider.GetRequiredService<IFileSystem>(),
            provider.GetRequiredService<ILogger<SiteBuilder>>())
        );

        services.TryAddSingleton<StaticFileResolverContract, StaticFileResolver>();
        services.TryAddSingleton<FileServerContract, FileServer>();

        services.TryAddSingleton<SiteWatcherContract>(provider => new SiteWatcher(
            provider.GetRequiredService<SiteBuilderContract>(),
            provider.GetRequiredService<IConfigurationRoot>(),
            provider.GetRequiredService<ConfigurationFileStatus>(),
            contentRoot,
            provider.GetRequiredService<IOptionsMonitor<SiteOptions>>(),
            provider.GetRequiredKeyedService<IAnsiConsole>(ConsoleTarget.StdOut),
            provider.GetRequiredKeyedService<IAnsiConsole>(ConsoleTarget.StdErr),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<IFileSystem>(),
            provider.GetRequiredService<ILogger<SiteWatcher>>())
        );

        return services;
    }
}
