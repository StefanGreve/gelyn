using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Gelyn.Extensions;

/// <summary>
///     The host the composition root in <see cref="Program"/> builds the tool on.
/// </summary>
internal static class HostExtensions
{
    private const string HostVariablePrefix = "DOTNET_";

    /// <summary>
    ///     Creates a host builder with no pre-configured defaults, seeding the host configuration with the
    ///     <c>DOTNET_</c> environment variables.
    /// </summary>
    /// <remarks>
    ///     The command line is left out, because its configuration provider does not parse the syntax
    ///     System.CommandLine accepts. The values it carries come from a System.CommandLine parse instead.
    /// </remarks>
    /// <returns>
    ///     The new builder, whose <see cref="HostApplicationBuilder.Configuration"/> is the seeded instance.
    /// </returns>
    public static HostApplicationBuilder CreateLeanApplicationBuilder()
    {
        var configuration = new ConfigurationManager();
        configuration.AddEnvironmentVariables(prefix: HostVariablePrefix);

        // The host resolves a relative content root against the tool's install directory, not the caller's.
        string? contentRoot = configuration[HostDefaults.ContentRootKey];

        return Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings
        {
            Configuration = configuration,
            ContentRootPath = string.IsNullOrEmpty(contentRoot) ? Directory.GetCurrentDirectory() : Path.GetFullPath(contentRoot),
        });
    }
}
