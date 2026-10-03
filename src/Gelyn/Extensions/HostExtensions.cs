using System;
using System.IO;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Gelyn.Extensions;

/// <summary>
///     The host the composition root in <see cref="Program"/> builds the tool on.
/// </summary>
public static class HostExtensions
{
    private const string HostVariablePrefix = "DOTNET_";

    /// <summary>
    ///     Creates a host builder with no pre-configured defaults, seeding the host configuration with the
    ///     environment variables and the command line.
    /// </summary>
    /// <param name="args">
    ///     Command line arguments passed to the tool.
    /// </param>
    /// <returns>
    ///     The new builder, whose <see cref="HostApplicationBuilder.Configuration"/> is the seeded instance.
    /// </returns>
    public static HostApplicationBuilder CreateLeanApplicationBuilder(this string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        ConfigurationManager configuration = new();

        configuration
            .AddEnvironmentVariables(prefix: HostVariablePrefix)
            .AddCommandLine(args);

        return Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings
        {
            Configuration = configuration,
            ContentRootPath = configuration[HostDefaults.ContentRootKey] ?? Directory.GetCurrentDirectory(),
        });
    }
}
