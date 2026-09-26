using System;
using System.Collections.Generic;

using Gelyn.Internals;
using Gelyn.Model.Options;

using Microsoft.Extensions.Configuration;

namespace Gelyn.Extensions;

/// <summary>
///     Configuration sources the composition root in <see cref="Program"/> layers on top of the
///     host defaults.
/// </summary>
public static class ConfigurationManagerExtensions
{
    /// <summary>
    ///     Adds the tool's configuration file and re-applies the sources that outrank it.
    /// </summary>
    /// <remarks>
    ///     A path supplied through <c>--config</c> must exist, whereas the conventional
    ///     <c>gelyn.json</c> is optional, because the defaults on <see cref="SiteOptions"/> are usable on
    ///     their own. The environment variables and the command line are added again so that they keep
    ///     overriding the file, which is added after them and would otherwise win.
    /// </remarks>
    /// <param name="configuration">
    ///     The configuration to add the sources to.
    /// </param>
    /// <param name="args">
    ///     Command line arguments passed to the tool.
    /// </param>
    /// <returns>
    ///     The same <see cref="ConfigurationManager"/> instance, so that calls can be chained.
    /// </returns>
    public static ConfigurationManager AddSiteConfiguration(this ConfigurationManager configuration, string[] args)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        string? requested = configuration[ConfigurationFile.PathKey];
        Dictionary<string, string?> overrides = configuration.ReadOverrides();

        configuration.AddJsonFile(
            requested ?? ConfigurationFile.DefaultFileName,
            optional: requested is null,
            reloadOnChange: true);

        configuration
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .AddInMemoryCollection(overrides);

        return configuration;
    }

    #region Helpers

    private static Dictionary<string, string?> ReadOverrides(this ConfigurationManager configuration)
    {
        (string Option, string Key)[] promotions =
        [
            (OverrideOptions.Title, nameof(SiteOptions.Title)),
            (OverrideOptions.Content, nameof(SiteOptions.ContentDirectory)),
            (OverrideOptions.Output, nameof(SiteOptions.OutputDirectory)),
        ];

        Dictionary<string, string?> overrides = [];

        foreach ((string option, string key) in promotions)
        {
            if (configuration[option] is string value)
                overrides[key] = value;
        }

        return overrides;
    }

    #endregion
}
