using Gelyn.Commands;

using Microsoft.Extensions.DependencyInjection;

using Spectre.Console;

namespace Gelyn.Extensions;

/// <summary>
///     Composition root for the <c>gelyn</c> tool.
/// </summary>
internal static class ServiceCollectionExtensions
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
    internal static IServiceCollection AddGelyn(this IServiceCollection services)
    {
        services.AddSingleton(AnsiConsole.Console);

        services.AddSingleton<GelynCommand>();

        return services;
    }
}
