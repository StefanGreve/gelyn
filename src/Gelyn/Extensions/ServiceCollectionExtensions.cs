using Gelyn.Abstractions;
using Gelyn.Commands;
using Gelyn.Components;
using Gelyn.Internals;
using Gelyn.Model.Options;
using Gelyn.Services;
using Gelyn.Validators;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using Spectre.Console;

namespace Gelyn.Extensions;

/// <summary>
///     Composition root for the <c>gelyn</c> tool.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Registers the tool's commands and the services they depend on.
    /// </summary>
    /// <param name="services">
    ///     The collection to add the registrations to.
    /// </param>
    /// <returns>
    ///     The same <see cref="IServiceCollection"/> instance, so that calls can be chained.
    /// </returns>
    public static IServiceCollection AddGelyn(this IServiceCollection services)
    {
        services.TryAddSingleton(AnsiConsole.Console);

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<SiteOptions>, SiteOptionsValidator>());

        services.AddOptions<SiteOptions>()
            .BindConfiguration(Sections.Site);

        services.TryAddSingleton<MarkdownRendererContract, MarkdigRenderer>();
        services.TryAddSingleton<HeaderComponent>();
        services.TryAddSingleton<FooterComponent>();
        services.TryAddSingleton<PageLayout>();

        services.TryAddSingleton<PageBuilderContract, HomePageBuilder>();
        services.TryAddSingleton<SiteBuilder>();

        services.TryAddSingleton<BuildCommand>();
        services.TryAddSingleton<GelynCommand>();

        return services;
    }
}