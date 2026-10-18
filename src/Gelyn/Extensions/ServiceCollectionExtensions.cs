using Gelyn.Commands;
using Gelyn.Model.Options;
using Gelyn.Services;
using Gelyn.Validators;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Gelyn.Extensions;

/// <summary>
///     Registration groups the composition root in <see cref="Program"/> is assembled from.
/// </summary>
internal static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Registers <see cref="SiteOptions"/> together with the validator that checks it.
    /// </summary>
    /// <remarks>
    ///     Bound to the root of the configuration rather than to a section, because an empty path binds the
    ///     whole configuration and <c>gelyn.json</c> therefore needs no wrapping object. Validation is not
    ///     wired to startup, so it runs when the options are first read.
    /// </remarks>
    /// <param name="services">
    ///     The collection to add the registrations to.
    /// </param>
    /// <returns>
    ///     The same <see cref="IServiceCollection"/> instance, so that calls can be chained.
    /// </returns>
    public static IServiceCollection AddSiteOptions(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<SiteOptions>, SiteOptionsValidator>());

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
    /// <returns>
    ///     The same <see cref="IServiceCollection"/> instance, so that calls can be chained.
    /// </returns>
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.TryAddSingleton<ContentPipeline>();
        services.TryAddSingleton<SiteBuilder>();
        services.TryAddSingleton<StaticFileResolver>();
        services.TryAddSingleton<FileServer>();
        services.TryAddSingleton<SiteWatcher>();

        return services;
    }

    /// <summary>
    ///     Registers the root command and every subcommand it dispatches to.
    /// </summary>
    /// <remarks>
    ///     <see cref="Program"/> resolves only the root command, and the subcommands reach it through its
    ///     constructor, so each one is registered here rather than constructed by hand.
    /// </remarks>
    /// <param name="services">
    ///     The collection to add the registrations to.
    /// </param>
    /// <returns>
    ///     The same <see cref="IServiceCollection"/> instance, so that calls can be chained.
    /// </returns>
    public static IServiceCollection AddCommands(this IServiceCollection services)
    {
        services.TryAddSingleton<BuildCommand>();
        services.TryAddSingleton<CleanCommand>();
        services.TryAddSingleton<ServeCommand>();
        services.TryAddSingleton<GelynCommand>();

        return services;
    }
}
